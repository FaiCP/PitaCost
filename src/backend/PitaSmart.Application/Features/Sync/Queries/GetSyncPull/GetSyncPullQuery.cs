#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Sync.Queries.GetSyncPull;

/// <summary>
/// Query para obtener todos los datos necesarios para popular RxDB en el cliente.
/// Endpoint: GET /v1/api/sync/pull
/// </summary>
public record GetSyncPullQuery : IRequest<ApiResponse<SyncPullResponse>>
{
}

/// <summary>Respuesta completa de sync pull para el cliente.</summary>
public record SyncPullResponse
{
    public DateTimeOffset ServerTimestamp { get; init; }
    public IReadOnlyList<SyncLoteDto> Lotes { get; init; } = [];
    public IReadOnlyList<SyncInsumoDto> Insumos { get; init; } = [];
    public IReadOnlyList<SyncAplicacionDto> Aplicaciones { get; init; } = [];
    public IReadOnlyList<SyncCosechaDto> Cosechas { get; init; } = [];
    public IReadOnlyList<SyncCostoDto> Costos { get; init; } = [];
}

public record SyncLoteDto
{
    public Guid Id { get; init; }
    public Guid FincaId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Cultivo { get; init; } = string.Empty;
    public decimal AreaHa { get; init; }
    public double? UbicacionLatitud { get; init; }
    public double? UbicacionLongitud { get; init; }
    public DateOnly? FechaInicioSiembra { get; init; }
    public bool Activo { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}

public record SyncInsumoDto
{
    public Guid Id { get; init; }
    public string NombreComercial { get; init; } = string.Empty;
    public string IngredienteActivo { get; init; } = string.Empty;
    public string TipoProducto { get; init; } = string.Empty;
    public string CategoriaToxico { get; init; } = string.Empty;
    public decimal ConcentracionValor { get; init; }
    public string ConcentracionUnidad { get; init; } = string.Empty;
    public decimal DosisMinima { get; init; }
    public decimal DosisMaxima { get; init; }
    public string UnidadDosis { get; init; } = string.Empty;
    public string PeriodoCarenciaJson { get; init; } = "[]";
    public bool Activo { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public record SyncAplicacionDto
{
    public Guid Id { get; init; }
    public Guid LoteId { get; init; }
    public Guid InsumoId { get; init; }
    public DateTimeOffset FechaAplicacion { get; init; }
    public decimal DosisCantidad { get; init; }
    public string DosisUnidad { get; init; } = string.Empty;
    public decimal AreaAplicadaHa { get; init; }
    public string MetodoAplicacion { get; init; } = string.Empty;
    public string OperadorNombre { get; init; } = string.Empty;
    public decimal CostoTotal { get; init; }
    public int DiasCarenciaAplicables { get; init; }
    public DateTimeOffset FechaFinCarencia { get; init; }
    public bool CreadoOffline { get; init; }
    public DateTimeOffset ClientTimestamp { get; init; }
    public string LoteNombre { get; init; } = string.Empty;
    public string InsumoNombre { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public record SyncCosechaDto
{
    public Guid Id { get; init; }
    public Guid LoteId { get; init; }
    public DateTimeOffset FechaCosecha { get; init; }
    public decimal PesoTotalKg { get; init; }
    public string CalidadGrado { get; init; } = string.Empty;
    public string? Comprador { get; init; }
    public decimal? PrecioVentaKg { get; init; }
    public decimal? IngresoTotal { get; init; }
    public bool BloqueadaPorCarencia { get; init; }
    public bool CreadoOffline { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public record SyncCostoDto
{
    public Guid Id { get; init; }
    public Guid LoteId { get; init; }
    public DateOnly Fecha { get; init; }
    public string Categoria { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal Monto { get; init; }
    public Guid? AplicacionId { get; init; }
    public Guid? CosechaId { get; init; }
    public bool CreadoOffline { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}
