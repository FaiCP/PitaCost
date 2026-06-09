#nullable enable
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Domain.Identidad.Entities;
using PitaSmart.Domain.Identidad.Interfaces;

namespace PitaSmart.Infrastructure.Identity;

/// <summary>
/// Servicio de autenticacion WebAuthn usando Fido2NetLib.
/// Gestiona el ciclo completo de registro y autenticacion de Passkeys.
/// NOTA: Los challenges se almacenan en memoria (Dictionary). En produccion,
/// usar Redis con TTL de 60 segundos para soporte multi-instancia.
/// </summary>
public class PasskeyService
{
    private readonly IFido2 _fido2;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<PasskeyService> _logger;

    // En produccion: Redis con TTL de 60s.
    private static readonly Dictionary<string, (CredentialCreateOptions Options, string Email)> _creationChallenges = new();
    private static readonly Dictionary<string, (AssertionOptions Options, string Email)> _assertionChallenges = new();

    public PasskeyService(
        IFido2 fido2,
        IUsuarioRepository usuarioRepository,
        IApplicationDbContext dbContext,
        ILogger<PasskeyService> logger)
    {
        _fido2 = fido2;
        _usuarioRepository = usuarioRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Genera un challenge WebAuthn para registrar una nueva Passkey.
    /// </summary>
    /// <param name="email">Email del usuario.</param>
    /// <returns>Tuple con challengeId y CredentialCreateOptions para el cliente.</returns>
    public (string ChallengeId, CredentialCreateOptions Options) GenerarChallengeRegistro(string email)
    {
        var user = new Fido2User
        {
            Id = System.Text.Encoding.UTF8.GetBytes(email),
            Name = email,
            DisplayName = email
        };

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = user,
            ExcludeCredentials = new List<PublicKeyCredentialDescriptor>(),
            AuthenticatorSelection = AuthenticatorSelection.Default,
            AttestationPreference = AttestationConveyancePreference.None
        });

        var challengeId = $"ch-{Guid.NewGuid()}";
        _creationChallenges[challengeId] = (options, email);

        _logger.LogInformation(
            "Challenge de registro generado para {Email}. ChallengeId: {ChallengeId}",
            email, challengeId);

        return (challengeId, options);
    }

    /// <summary>
    /// Genera un challenge WebAuthn para autenticar un usuario existente.
    /// </summary>
    /// <param name="email">Email del usuario.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <returns>Tuple con challengeId y AssertionOptions, o null si el usuario no existe.</returns>
    public async Task<(string ChallengeId, AssertionOptions Options)?> GenerarChallengeAutenticacion(
        string email, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByEmailAsync(email, cancellationToken);
        if (usuario is null)
        {
            _logger.LogWarning("Intento de autenticacion para email no registrado: {Email}", email);
            return null;
        }

        var allowedCredentials = usuario.Credenciales
            .Where(c => c.Activa)
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        if (allowedCredentials.Count == 0)
        {
            _logger.LogWarning("Usuario {Email} no tiene credenciales Passkey activas.", email);
            return null;
        }

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification = UserVerificationRequirement.Preferred
        });

        var challengeId = $"ch-{Guid.NewGuid()}";
        _assertionChallenges[challengeId] = (options, email);

        _logger.LogInformation(
            "Challenge de autenticacion generado para {Email}. ChallengeId: {ChallengeId}",
            email, challengeId);

        return (challengeId, options);
    }

    /// <summary>
    /// Verifica la respuesta del autenticador para registro de nueva Passkey.
    /// Persiste la credencial en la base de datos.
    /// </summary>
    /// <param name="challengeId">ID del challenge previamente generado.</param>
    /// <param name="attestationResponse">Respuesta del autenticador WebAuthn.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <returns>True si el registro fue exitoso.</returns>
    public async Task<bool> VerificarRegistroAsync(
        string challengeId,
        AuthenticatorAttestationRawResponse attestationResponse,
        CancellationToken cancellationToken = default)
    {
        if (!_creationChallenges.TryGetValue(challengeId, out var challenge))
        {
            _logger.LogWarning("Challenge de registro no encontrado: {ChallengeId}", challengeId);
            return false;
        }

        _creationChallenges.Remove(challengeId);

        try
        {
            var result = await _fido2.MakeNewCredentialAsync(
                new MakeNewCredentialParams
                {
                    AttestationResponse = attestationResponse,
                    OriginalOptions = challenge.Options,
                    IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                    {
                        var existing = await _usuarioRepository.GetCredencialByIdAsync(
                            args.CredentialId, ct);
                        return existing is null;
                    }
                },
                cancellationToken);

            var usuario = await _usuarioRepository.GetByEmailAsync(challenge.Email, cancellationToken);
            if (usuario is null)
                return false;

            var credencial = CredencialPasskey.Crear(
                usuarioId: usuario.Id,
                credentialId: result.Id,
                publicKey: result.PublicKey,
                signCount: result.SignCount,
                aaGuid: result.AaGuid,
                credentialType: result.Type.ToString(),
                dispositivoNombre: null);

            await _usuarioRepository.AddCredencialAsync(credencial, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Passkey registrada exitosamente para {Email}. CredentialId: {CredentialId}",
                challenge.Email, Convert.ToBase64String(result.Id));

            return true;
        }
        catch (Fido2VerificationException ex)
        {
            _logger.LogWarning(ex, "Fallo verificacion de registro Passkey para {Email}", challenge.Email);
            return false;
        }
    }

    /// <summary>
    /// Verifica la respuesta del autenticador para autenticacion.
    /// Actualiza el SignCount de la credencial.
    /// </summary>
    /// <param name="challengeId">ID del challenge previamente generado.</param>
    /// <param name="assertionResponse">Respuesta del autenticador WebAuthn.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <returns>El ID del usuario autenticado, o null si fallo la verificacion.</returns>
    public async Task<Guid?> VerificarAutenticacionAsync(
        string challengeId,
        AuthenticatorAssertionRawResponse assertionResponse,
        CancellationToken cancellationToken = default)
    {
        if (!_assertionChallenges.TryGetValue(challengeId, out var challenge))
        {
            _logger.LogWarning("Challenge de autenticacion no encontrado: {ChallengeId}", challengeId);
            return null;
        }

        _assertionChallenges.Remove(challengeId);

        var usuario = await _usuarioRepository.GetByEmailAsync(challenge.Email, cancellationToken);
        if (usuario is null)
            return null;

        var credencial = usuario.Credenciales.FirstOrDefault(c => c.Activa);
        if (credencial is null)
            return null;

        try
        {
            var assertionResult = await _fido2.MakeAssertionAsync(
                new MakeAssertionParams
                {
                    AssertionResponse = assertionResponse,
                    OriginalOptions = challenge.Options,
                    StoredPublicKey = credencial.PublicKey,
                    StoredSignatureCounter = credencial.SignCount,
                    IsUserHandleOwnerOfCredentialIdCallback = (args, ct) => Task.FromResult(true)
                },
                cancellationToken);

            // Actualizar SignCount para proteccion anti-clone.
            credencial.ActualizarSignCount(assertionResult.SignCount);
            usuario.RegistrarAcceso();
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Autenticacion Passkey exitosa para {Email} (UsuarioId: {UsuarioId})",
                challenge.Email, usuario.Id);

            return usuario.Id;
        }
        catch (Fido2VerificationException ex)
        {
            _logger.LogWarning(ex,
                "Fallo verificacion de autenticacion Passkey para {Email}", challenge.Email);
            return null;
        }
    }
}
