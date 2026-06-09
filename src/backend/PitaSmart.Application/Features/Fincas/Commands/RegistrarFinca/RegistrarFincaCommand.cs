#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Fincas.Commands.RegistrarFinca;

/// <summary>
/// Command para registrar una nueva finca asociada al usuario autenticado.
/// </summary>
public record RegistrarFincaCommand : IRequest<ApiResponse<RegistrarFincaResponse>>
{
    /// <summary>Nombre de la finca.</summary>
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Provincia donde se ubica la finca.</summary>
    public string Provincia { get; init; } = string.Empty;

    /// <summary>Cantón donde se ubica la finca.</summary>
    public string Canton { get; init; } = string.Empty;

    /// <summary>Parroquia (opcional).</summary>
    public string? Parroquia { get; init; }

    /// <summary>Área total en hectáreas.</summary>
    public decimal AreaTotalHa { get; init; }
}

/// <summary>Respuesta al registrar una finca.</summary>
public record RegistrarFincaResponse
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Provincia { get; init; } = string.Empty;
    public string Canton { get; init; } = string.Empty;
    public string? Parroquia { get; init; }
    public decimal AreaTotalHa { get; init; }
}
