#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Identidad.Entities;

/// <summary>
/// Credencial WebAuthn/Passkey registrada para un usuario.
/// </summary>
public class CredencialPasskey : BaseEntity
{
    public Guid UsuarioId { get; private set; }
    public byte[] CredentialId { get; private set; } = [];
    public byte[] PublicKey { get; private set; } = [];
    public uint SignCount { get; private set; }
    public Guid AaGuid { get; private set; }
    public string CredentialType { get; private set; } = "public-key";
    public DateTimeOffset FechaRegistro { get; private set; }
    public string? DispositivoNombre { get; private set; }
    public bool Activa { get; private set; } = true;

    public Usuario Usuario { get; private set; } = null!;

    private CredencialPasskey() { }

    /// <summary>
    /// Factory method para crear una nueva credencial Passkey.
    /// </summary>
    public static CredencialPasskey Crear(
        Guid usuarioId,
        byte[] credentialId,
        byte[] publicKey,
        uint signCount,
        Guid aaGuid,
        string credentialType,
        string? dispositivoNombre)
    {
        return new CredencialPasskey
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            CredentialId = credentialId,
            PublicKey = publicKey,
            SignCount = signCount,
            AaGuid = aaGuid,
            CredentialType = credentialType,
            FechaRegistro = DateTimeOffset.UtcNow,
            DispositivoNombre = dispositivoNombre,
            Activa = true
        };
    }

    /// <summary>Actualiza el contador de firmas (proteccion anti-clone).</summary>
    public void ActualizarSignCount(uint newCount) => SignCount = newCount;
}
