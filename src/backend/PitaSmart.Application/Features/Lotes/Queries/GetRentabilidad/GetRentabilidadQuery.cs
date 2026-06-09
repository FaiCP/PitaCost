#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Lotes.Queries.GetRentabilidad;

/// <summary>
/// Query para obtener el cálculo de rentabilidad de un lote en un período.
/// GET /v1/api/lotes/{id}/rentabilidad?desde=...&amp;hasta=...
/// </summary>
public record GetRentabilidadQuery : IRequest<ApiResponse<RentabilidadResponse>>
{
    public Guid LoteId { get; init; }
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }
}

/// <summary>Respuesta completa de rentabilidad según contrato API.</summary>
public record RentabilidadResponse
{
    public Guid LoteId { get; init; }
    public string LoteNombre { get; init; } = string.Empty;
    public string CultivoActual { get; init; } = string.Empty;
    public decimal AreaHa { get; init; }
    public PeriodoDto Periodo { get; init; } = null!;
    public IngresosDto Ingresos { get; init; } = null!;
    public CostosDto Costos { get; init; } = null!;
    public RentabilidadCalculoDto Rentabilidad { get; init; } = null!;
    public List<AlertaDto> Alertas { get; init; } = [];
}

public record PeriodoDto(string Desde, string Hasta);

public record IngresosDto
{
    public decimal TotalVentas { get; init; }
    public decimal PrecioPromedioKg { get; init; }
    public decimal TotalKgCosechados { get; init; }
    public List<DetalleVentaDto> DetalleVentas { get; init; } = [];
}

public record DetalleVentaDto
{
    public string Fecha { get; init; } = string.Empty;
    public string Comprador { get; init; } = string.Empty;
    public decimal KgVendidos { get; init; }
    public decimal PrecioKg { get; init; }
    public decimal TotalVenta { get; init; }
}

public record CostosDto
{
    public decimal TotalCostos { get; init; }
    public decimal CostoPorHa { get; init; }
    public decimal CostoPorKg { get; init; }
    public List<DesgloseCategoriaDto> Desglose { get; init; } = [];
}

public record DesgloseCategoriaDto
{
    public string Categoria { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public decimal Porcentaje { get; init; }
    public List<ItemCostoDto> Items { get; init; } = [];
}

public record ItemCostoDto
{
    public string Descripcion { get; init; } = string.Empty;
    public decimal Total { get; init; }
}

public record RentabilidadCalculoDto
{
    public decimal UtilidadBruta { get; init; }
    public decimal MargenBruto { get; init; }
    public decimal UtilidadPorHa { get; init; }
    public decimal UtilidadPorKg { get; init; }
    public decimal Roi { get; init; }
}

public record AlertaDto
{
    public string Tipo { get; init; } = string.Empty;
    public string Mensaje { get; init; } = string.Empty;
    public string Severidad { get; init; } = string.Empty;
}
