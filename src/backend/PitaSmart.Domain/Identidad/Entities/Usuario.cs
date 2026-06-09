#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Identidad.Entities;

/// <summary>
/// Usuario del sistema PitaSmart. Aggregate Root del bounded context Identidad.
/// </summary>
public class Usuario : BaseEntity, IAuditableEntity
{
    public string Email { get; private set; } = string.Empty;
    public string NombreCompleto { get; private set; } = string.Empty;
    public string? Cedula { get; private set; }
    public string? Telefono { get; private set; }

    /// <summary>Rol: AGRICULTOR, ADMINISTRADOR, AUDITOR.</summary>
    public string Rol { get; private set; } = "AGRICULTOR";

    /// <summary>Hash BCrypt de la contraseña. Null si el usuario solo usa Passkey.</summary>
    public string? PasswordHash { get; private set; }

    public bool Activo { get; private set; } = true;
    public DateTimeOffset FechaRegistro { get; private set; }
    public DateTimeOffset? UltimoAcceso { get; private set; }

    public ICollection<CredencialPasskey> Credenciales { get; private set; } = new List<CredencialPasskey>();
    public ICollection<SesionDispositivo> Sesiones { get; private set; } = new List<SesionDispositivo>();

    private Usuario() { }

    /// <summary>Crea un nuevo usuario con datos de registro.</summary>
    public static Usuario Crear(
        Guid id,
        string email,
        string nombreCompleto,
        string? cedula,
        string? telefono,
        string rol = "AGRICULTOR")
    {
        return new Usuario
        {
            Id = id,
            Email = email,
            NombreCompleto = nombreCompleto,
            Cedula = cedula,
            Telefono = telefono,
            Rol = rol,
            Activo = true,
            FechaRegistro = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Crea un usuario con contraseña hasheada.</summary>
    public static Usuario CrearConPassword(
        Guid id,
        string email,
        string nombreCompleto,
        string passwordHash,
        string? cedula = null,
        string? telefono = null)
    {
        return new Usuario
        {
            Id = id,
            Email = email,
            NombreCompleto = nombreCompleto,
            PasswordHash = passwordHash,
            Cedula = cedula,
            Telefono = telefono,
            Rol = "AGRICULTOR",
            Activo = true,
            FechaRegistro = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Actualiza la contraseña del usuario.</summary>
    public void ActualizarPassword(string nuevoHash) => PasswordHash = nuevoHash;

    /// <summary>Registra el último acceso exitoso.</summary>
    public void RegistrarAcceso() => UltimoAcceso = DateTimeOffset.UtcNow;
}
