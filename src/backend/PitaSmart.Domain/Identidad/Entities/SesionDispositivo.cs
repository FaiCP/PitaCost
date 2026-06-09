#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Identidad.Entities;

/// <summary>
/// Sesión activa de un dispositivo con refresh token.
/// </summary>
public class SesionDispositivo : BaseEntity
{
    public Guid UsuarioId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public string? Plataforma { get; private set; }
    public string? AppVersion { get; private set; }
    public DateTimeOffset FechaCreacion { get; private set; }
    public DateTimeOffset FechaExpiracion { get; private set; }
    public bool Activa { get; private set; } = true;

    public Usuario Usuario { get; private set; } = null!;

    private SesionDispositivo() { }

    /// <summary>Revoca la sesión.</summary>
    public void Revocar() => Activa = false;
}
