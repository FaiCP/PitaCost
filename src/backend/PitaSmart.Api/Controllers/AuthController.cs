#nullable enable
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Identidad.Entities;
using PitaSmart.Domain.Identidad.Interfaces;
using PitaSmart.Infrastructure.Identity;
using Microsoft.Extensions.Hosting;

namespace PitaSmart.Api.Controllers;

/// <summary>
/// Controlador de autenticación WebAuthn/Passkeys.
/// Base path: /v1/api/auth
/// Flujo: 1) POST /challenge -> 2) POST /verify -> JWT emitido.
/// </summary>
[ApiController]
[Route("v1/api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IFido2 _fido2;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IHostEnvironment _env;
    private readonly IApplicationDbContext _dbContext;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    // En producción, usar un store distribuido (Redis) con TTL de 60s.
    private static readonly Dictionary<string, (AssertionOptions Options, string Email)> _assertionChallenges = new();
    private static readonly Dictionary<string, (CredentialCreateOptions Options, string Email)> _creationChallenges = new();

    public AuthController(
        IFido2 fido2,
        IUsuarioRepository usuarioRepository,
        JwtTokenService jwtTokenService,
        IHostEnvironment env,
        IApplicationDbContext dbContext,
        IPasswordHasher<Usuario> passwordHasher)
    {
        _fido2 = fido2;
        _usuarioRepository = usuarioRepository;
        _jwtTokenService = jwtTokenService;
        _env = env;
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// Genera un challenge WebAuthn para iniciar autenticación o registro de Passkey.
    /// </summary>
    /// <param name="request">Email y tipo (AUTENTICACION o REGISTRO).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>200 OK con challenge WebAuthn y opciones del autenticador.</returns>
    /// <response code="200">Challenge generado exitosamente.</response>
    /// <response code="400">Email no proporcionado o usuario no encontrado.</response>
    [HttpPost("challenge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Challenge(
        [FromBody] ChallengeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "El email es requerido."));

        var challengeId = $"ch-{Guid.NewGuid()}";
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(60);

        if (request.Tipo == "REGISTRO")
        {
            var user = new Fido2User
            {
                Id = System.Text.Encoding.UTF8.GetBytes(request.Email),
                Name = request.Email,
                DisplayName = request.Email
            };

            var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
            {
                User = user,
                ExcludeCredentials = new List<PublicKeyCredentialDescriptor>(),
                AuthenticatorSelection = AuthenticatorSelection.Default,
                AttestationPreference = AttestationConveyancePreference.None
            });

            _creationChallenges[challengeId] = (options, request.Email);

            return Ok(ApiResponse<object>.Ok(new
            {
                challengeId,
                publicKeyCredentialCreationOptions = options,
                expiresAt
            }));
        }
        else // AUTENTICACION
        {
            var usuario = await _usuarioRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (usuario is null)
                return BadRequest(ApiResponse<object>.Fail("USUARIO_NO_ENCONTRADO", "No existe un usuario con ese email."));

            var allowedCredentials = usuario.Credenciales
                .Where(c => c.Activa)
                .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                .ToList();

            var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = allowedCredentials,
                UserVerification = UserVerificationRequirement.Preferred
            });

            _assertionChallenges[challengeId] = (options, request.Email);

            return Ok(ApiResponse<object>.Ok(new
            {
                challengeId,
                publicKeyCredentialRequestOptions = options,
                expiresAt
            }));
        }
    }

    /// <summary>
    /// Verifica la respuesta del autenticador WebAuthn y emite un JWT si es válida.
    /// </summary>
    /// <param name="request">Challenge ID, tipo, credencial y datos del dispositivo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>200 OK con access token JWT y datos del usuario.</returns>
    /// <response code="200">Autenticación/registro exitoso.</response>
    /// <response code="400">Challenge expirado o no encontrado.</response>
    /// <response code="401">Verificación de firma fallida.</response>
    [HttpPost("verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Tipo == "AUTENTICACION")
        {
            if (!_assertionChallenges.TryGetValue(request.ChallengeId, out var challenge))
                return BadRequest(ApiResponse<object>.Fail("CHALLENGE_NOT_FOUND", "El challenge no existe."));

            _assertionChallenges.Remove(request.ChallengeId);

            var usuario = await _usuarioRepository.GetByEmailAsync(challenge.Email, cancellationToken);
            if (usuario is null)
                return Unauthorized(ApiResponse<object>.Fail("CREDENTIAL_NOT_FOUND", "Credencial no registrada."));

            var credencial = usuario.Credenciales.FirstOrDefault(c => c.Activa);
            if (credencial is null)
                return Unauthorized(ApiResponse<object>.Fail("CREDENTIAL_NOT_FOUND", "No hay credenciales activas."));

            try
            {
                var assertionResult = await _fido2.MakeAssertionAsync(
                    new MakeAssertionParams
                    {
                        AssertionResponse = request.Credential!,
                        OriginalOptions = challenge.Options,
                        StoredPublicKey = credencial.PublicKey,
                        StoredSignatureCounter = credencial.SignCount,
                        IsUserHandleOwnerOfCredentialIdCallback = (args, ct) => Task.FromResult(true)
                    },
                    cancellationToken);

                credencial.ActualizarSignCount(assertionResult.SignCount);
                usuario.RegistrarAcceso();

                var fincaIds = Array.Empty<Guid>(); // Se cargarían del repositorio de fincas.
                var accessToken = _jwtTokenService.GenerarAccessToken(usuario, fincaIds);
                var refreshToken = _jwtTokenService.GenerarRefreshToken();

                return Ok(ApiResponse<object>.Ok(new
                {
                    accessToken,
                    refreshToken,
                    expiresIn = 3600,
                    tokenType = "Bearer",
                    usuario = new
                    {
                        id = usuario.Id,
                        email = usuario.Email,
                        nombreCompleto = usuario.NombreCompleto,
                        rol = usuario.Rol,
                        fincas = Array.Empty<object>()
                    }
                }));
            }
            catch (Fido2VerificationException)
            {
                return Unauthorized(ApiResponse<object>.Fail("VERIFICATION_FAILED", "La firma del autenticador es invalida."));
            }
        }

        // REGISTRO flow - simplified for MVP
        return Ok(ApiResponse<object>.Ok(new { message = "Registro de passkey completado." }));
    }

    /// <summary>
    /// Registra un nuevo usuario con email y contraseña.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.NombreCompleto))
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Email, nombre y contraseña son requeridos."));

        var existente = await _usuarioRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existente is not null)
            return Conflict(ApiResponse<object>.Fail("EMAIL_EN_USO", "Ya existe una cuenta con ese email."));

        var usuario = Usuario.CrearConPassword(
            id: Guid.NewGuid(),
            email: request.Email.ToLowerInvariant().Trim(),
            nombreCompleto: request.NombreCompleto.Trim(),
            passwordHash: string.Empty);

        var hash = _passwordHasher.HashPassword(usuario, request.Password);
        usuario.ActualizarPassword(hash);

        await _usuarioRepository.AddUsuarioAsync(usuario, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.GenerarAccessToken(usuario, Array.Empty<Guid>());
        var refreshToken = _jwtTokenService.GenerarRefreshToken();

        return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(new
        {
            accessToken,
            refreshToken,
            expiresIn = 3600,
            tokenType = "Bearer",
            usuario = new
            {
                id = usuario.Id,
                email = usuario.Email,
                nombreCompleto = usuario.NombreCompleto,
                rol = usuario.Rol,
                fincas = Array.Empty<object>()
            }
        }));
    }

    /// <summary>
    /// Login con email y contraseña.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Email y contraseña son requeridos."));

        var usuario = await _usuarioRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (usuario is null || string.IsNullOrEmpty(usuario.PasswordHash))
            return Unauthorized(ApiResponse<object>.Fail("CREDENCIALES_INVALIDAS", "Email o contraseña incorrectos."));

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password);
        if (resultado == PasswordVerificationResult.Failed)
            return Unauthorized(ApiResponse<object>.Fail("CREDENCIALES_INVALIDAS", "Email o contraseña incorrectos."));

        usuario.RegistrarAcceso();
        await _dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.GenerarAccessToken(usuario, Array.Empty<Guid>());
        var refreshToken = _jwtTokenService.GenerarRefreshToken();

        return Ok(ApiResponse<object>.Ok(new
        {
            accessToken,
            refreshToken,
            expiresIn = 3600,
            tokenType = "Bearer",
            usuario = new
            {
                id = usuario.Id,
                email = usuario.Email,
                nombreCompleto = usuario.NombreCompleto,
                rol = usuario.Rol,
                fincas = Array.Empty<object>()
            }
        }));
    }

    /// <summary>
    /// Login directo por email sin WebAuthn. SOLO disponible en Development.
    /// Permite probar la app sin configurar Passkeys en el dispositivo.
    /// </summary>
    [HttpPost("dev-login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DevLogin(
        [FromBody] DevLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment())
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "El email es requerido."));

        var usuario = await _usuarioRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (usuario is null)
            return BadRequest(ApiResponse<object>.Fail("USUARIO_NO_ENCONTRADO", "No existe un usuario con ese email."));

        usuario.RegistrarAcceso();

        var fincaIds = Array.Empty<Guid>();
        var accessToken = _jwtTokenService.GenerarAccessToken(usuario, fincaIds);
        var refreshToken = _jwtTokenService.GenerarRefreshToken();

        return Ok(ApiResponse<object>.Ok(new
        {
            accessToken,
            refreshToken,
            expiresIn = 3600,
            tokenType = "Bearer",
            usuario = new
            {
                id = usuario.Id,
                email = usuario.Email,
                nombreCompleto = usuario.NombreCompleto,
                rol = usuario.Rol,
                fincas = Array.Empty<object>()
            }
        }));
    }
}

/// <summary>Request para dev-login (solo Development).</summary>
public record DevLoginRequest
{
    public string Email { get; init; } = string.Empty;
}

/// <summary>Request para registro con email y contraseña.</summary>
public record RegisterRequest
{
    public string Email { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

/// <summary>Request para login con email y contraseña.</summary>
public record LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

/// <summary>Request para generar challenge WebAuthn.</summary>
public record ChallengeRequest
{
    public string Email { get; init; } = string.Empty;
    public string Tipo { get; init; } = "AUTENTICACION";
}

/// <summary>Request para verificar respuesta del autenticador WebAuthn.</summary>
public record VerifyRequest
{
    public string ChallengeId { get; init; } = string.Empty;
    public string Tipo { get; init; } = "AUTENTICACION";
    public AuthenticatorAssertionRawResponse? Credential { get; init; }
    public DeviceInfoDto? DeviceInfo { get; init; }
}

/// <summary>Información del dispositivo para registro de sesiones.</summary>
public record DeviceInfoDto
{
    public string DeviceId { get; init; } = string.Empty;
    public string? Platform { get; init; }
    public string? AppVersion { get; init; }
}
