#nullable enable
namespace PitaSmart.Application.Common.Interfaces;

/// <summary>
/// Servicio que expone datos del usuario autenticado actual.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>ID del usuario autenticado.</summary>
    Guid UserId { get; }

    /// <summary>Email del usuario autenticado.</summary>
    string Email { get; }

    /// <summary>Rol del usuario: AGRICULTOR, ADMINISTRADOR, AUDITOR.</summary>
    string Role { get; }

    /// <summary>IDs de fincas a las que tiene acceso.</summary>
    IReadOnlyList<Guid> FincaIds { get; }
}
