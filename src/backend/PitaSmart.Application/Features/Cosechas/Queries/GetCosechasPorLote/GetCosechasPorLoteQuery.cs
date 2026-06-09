#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Cosechas.Queries.GetCosechasPorLote;

/// <summary>
/// Query para obtener las cosechas de un lote especifico.
/// Endpoint: GET /v1/api/cosechas?loteId={id}
/// </summary>
public record GetCosechasPorLoteQuery : IRequest<ApiResponse<IReadOnlyList<CosechaDto>>>
{
    /// <summary>ID del lote a consultar.</summary>
    public Guid LoteId { get; init; }
}

/// <summary>
/// DTO de cosecha para la respuesta de consulta.
/// </summary>
public record CosechaDto
{
    public Guid Id { get; init; }
    public Guid LoteId { get; init; }
    public DateTimeOffset FechaCosecha { get; init; }
    public decimal PesoTotalKg { get; init; }
    public string CalidadGrado { get; init; } = string.Empty;
    public string? Comprador { get; init; }
    public decimal? PrecioVentaKg { get; init; }
    public decimal? IngresoTotal { get; init; }
    public string? Observaciones { get; init; }
    public bool BloqueadaPorCarencia { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
