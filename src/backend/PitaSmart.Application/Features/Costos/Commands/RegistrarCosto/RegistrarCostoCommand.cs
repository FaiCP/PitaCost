#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Costos.Commands.RegistrarCosto;

/// <summary>
/// Command para registrar un nuevo costo en un lote.
/// Soporta creación online y offline (idempotente por Id).
/// </summary>
public record RegistrarCostoCommand : IRequest<ApiResponse<RegistrarCostoResponse>>
{
    /// <summary>UUID generado en cliente (idempotency key).</summary>
    public Guid Id { get; init; }

    public Guid LoteId { get; init; }

    /// <summary>Fecha del costo (YYYY-MM-DD).</summary>
    public DateOnly Fecha { get; init; }

    /// <summary>Categoría: INSUMOS_QUIMICOS, MANO_DE_OBRA, TRANSPORTE, RIEGO, MAQUINARIA, OTROS.</summary>
    public string Categoria { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    /// <summary>Monto en USD. Debe ser mayor a 0.</summary>
    public decimal Monto { get; init; }

    /// <summary>ID de aplicación relacionada (opcional, para costos automáticos de insumos).</summary>
    public Guid? AplicacionId { get; init; }

    public bool CreadoOffline { get; init; }

    public DateTimeOffset ClientTimestamp { get; init; }
}

/// <summary>Respuesta al registrar un costo.</summary>
public record RegistrarCostoResponse
{
    public Guid Id { get; init; }
    public Guid LoteId { get; init; }
    public string LoteNombre { get; init; } = string.Empty;
    public DateOnly Fecha { get; init; }
    public string Categoria { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal Monto { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
