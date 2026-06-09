#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Insumos.Queries.GetInsumos;

/// <summary>
/// Query para obtener el catálogo de insumos activos con filtros opcionales.
/// Endpoint: GET /v1/api/insumos
/// </summary>
public record GetInsumosQuery : IRequest<ApiResponse<IReadOnlyList<InsumoDto>>>
{
    /// <summary>Filtro opcional por tipo de producto.</summary>
    public string? TipoProducto { get; init; }

    /// <summary>Búsqueda opcional por nombre comercial o ingrediente activo.</summary>
    public string? Search { get; init; }
}

/// <summary>DTO de período de carencia para la respuesta.</summary>
public record PeriodoCarenciaDto
{
    public string Cultivo { get; init; } = string.Empty;
    public int DiasCarencia { get; init; }
}

/// <summary>DTO de insumo para la respuesta de consulta.</summary>
public record InsumoDto
{
    public Guid Id { get; init; }
    public string NombreComercial { get; init; } = string.Empty;
    public string IngredienteActivo { get; init; } = string.Empty;
    public string TipoProducto { get; init; } = string.Empty;
    public string CategoriaToxico { get; init; } = string.Empty;
    public decimal DosisMinima { get; init; }
    public decimal DosisMaxima { get; init; }
    public string UnidadDosis { get; init; } = string.Empty;
    public IReadOnlyList<PeriodoCarenciaDto> PeriodosCarencia { get; init; } = [];
}
