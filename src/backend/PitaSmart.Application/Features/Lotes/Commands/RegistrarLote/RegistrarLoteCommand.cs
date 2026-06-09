#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Lotes.Commands.RegistrarLote;

/// <summary>
/// Command para registrar un nuevo lote en una finca.
/// </summary>
public record RegistrarLoteCommand : IRequest<ApiResponse<RegistrarLoteResponse>>
{
    /// <summary>FK a la finca donde se ubica el lote.</summary>
    public Guid FincaId { get; init; }

    /// <summary>Nombre del lote.</summary>
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Cultivo principal del lote.</summary>
    public string Cultivo { get; init; } = string.Empty;

    /// <summary>Área en hectáreas.</summary>
    public decimal AreaHa { get; init; }

    /// <summary>Latitud de la ubicación (opcional).</summary>
    public double? Latitud { get; init; }

    /// <summary>Longitud de la ubicación (opcional).</summary>
    public double? Longitud { get; init; }

    /// <summary>Fecha de inicio de siembra (opcional).</summary>
    public DateOnly? FechaInicioSiembra { get; init; }
}

/// <summary>Respuesta al registrar un lote.</summary>
public record RegistrarLoteResponse
{
    public Guid Id { get; init; }
    public Guid FincaId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Cultivo { get; init; } = string.Empty;
    public decimal AreaHa { get; init; }
    public double? Latitud { get; init; }
    public double? Longitud { get; init; }
    public DateOnly? FechaInicioSiembra { get; init; }
    public bool Activo { get; init; }
}
