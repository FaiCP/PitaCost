#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Costos.Commands.ActualizarCosto;

/// <summary>
/// Command para actualizar un costo existente de un lote.
/// </summary>
public record ActualizarCostoCommand : IRequest<ApiResponse<ActualizarCostoResponse>>
{
    /// <summary>ID del costo a actualizar (viene de la ruta).</summary>
    public Guid Id { get; init; }

    /// <summary>Nueva descripción del costo.</summary>
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>Nuevo monto en USD.</summary>
    public decimal Monto { get; init; }

    /// <summary>Nueva categoría.</summary>
    public string Categoria { get; init; } = string.Empty;

    /// <summary>Nueva fecha del costo.</summary>
    public DateOnly Fecha { get; init; }
}

/// <summary>Respuesta al actualizar un costo.</summary>
public record ActualizarCostoResponse
{
    public Guid Id { get; init; }
    public Guid LoteId { get; init; }
    public DateOnly Fecha { get; init; }
    public string Categoria { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal Monto { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}
