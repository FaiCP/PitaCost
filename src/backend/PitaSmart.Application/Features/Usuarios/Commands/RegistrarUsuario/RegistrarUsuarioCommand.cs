#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Usuarios.Commands.RegistrarUsuario;

/// <summary>
/// Command para registrar un nuevo usuario en el sistema.
/// </summary>
public record RegistrarUsuarioCommand : IRequest<ApiResponse<RegistrarUsuarioResponse>>
{
    /// <summary>Email del usuario (único).</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Nombre completo del usuario.</summary>
    public string NombreCompleto { get; init; } = string.Empty;

    /// <summary>Cédula de identidad (opcional).</summary>
    public string? Cedula { get; init; }

    /// <summary>Teléfono de contacto (opcional).</summary>
    public string? Telefono { get; init; }
}

/// <summary>Respuesta al registrar un usuario.</summary>
public record RegistrarUsuarioResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string Rol { get; init; } = string.Empty;
}
