#nullable enable
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Sync.Queries.GetSyncPull;

/// <summary>
/// Handler para <see cref="GetSyncPullQuery"/>.
/// Retorna todos los datos necesarios para popular RxDB en el cliente.
/// Filtra por usuario autenticado para lotes, aplicaciones, cosechas y costos.
/// Los insumos son catálogo compartido (todos los activos).
/// </summary>
public class GetSyncPullQueryHandler
    : IRequestHandler<GetSyncPullQuery, ApiResponse<SyncPullResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetSyncPullQueryHandler> _logger;

    public GetSyncPullQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<GetSyncPullQueryHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<SyncPullResponse>> Handle(
        GetSyncPullQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;

        // Obtener los IDs de lotes del usuario para filtrar datos relacionados.
        var loteIds = await _dbContext.Lotes
            .Include(l => l.Finca)
            .Where(l => l.Finca.UsuarioId == userId)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        // Las consultas deben ejecutarse secuencialmente: EF Core no permite
        // múltiples operaciones concurrentes sobre la misma instancia de DbContext.
        var lotes = await GetLotesAsync(userId, cancellationToken);
        var insumos = await GetInsumosAsync(cancellationToken);
        var aplicaciones = await GetAplicacionesAsync(loteIds, cancellationToken);
        var cosechas = await GetCosechasAsync(loteIds, cancellationToken);
        var costos = await GetCostosAsync(loteIds, cancellationToken);

        var response = new SyncPullResponse
        {
            ServerTimestamp = DateTimeOffset.UtcNow,
            Lotes = lotes,
            Insumos = insumos,
            Aplicaciones = aplicaciones,
            Cosechas = cosechas,
            Costos = costos
        };

        _logger.LogInformation(
            "Sync pull para usuario {UserId}: {Lotes} lotes, {Insumos} insumos, {Apps} aplicaciones, {Cosechas} cosechas, {Costos} costos.",
            userId, response.Lotes.Count, response.Insumos.Count,
            response.Aplicaciones.Count, response.Cosechas.Count, response.Costos.Count);

        return ApiResponse<SyncPullResponse>.Ok(response);
    }

    private async Task<IReadOnlyList<SyncLoteDto>> GetLotesAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        // Convert.ToBase64String no es traducible a SQL: materializar primero, proyectar en memoria.
        var rows = await _dbContext.Lotes
            .Include(l => l.Finca)
            .Where(l => l.Finca.UsuarioId == userId)
            .Select(l => new
            {
                l.Id, l.FincaId, l.Nombre, l.Cultivo, l.AreaHa,
                l.Latitud, l.Longitud, l.FechaInicioSiembra, l.Activo,
                l.RowVersion, l.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(l => new SyncLoteDto
        {
            Id = l.Id,
            FincaId = l.FincaId,
            Nombre = l.Nombre,
            Cultivo = l.Cultivo,
            AreaHa = l.AreaHa,
            UbicacionLatitud = l.Latitud,
            UbicacionLongitud = l.Longitud,
            FechaInicioSiembra = l.FechaInicioSiembra,
            Activo = l.Activo,
            RowVersion = Convert.ToBase64String(l.RowVersion),
            UpdatedAt = l.UpdatedAt
        }).ToList();
    }

    private async Task<IReadOnlyList<SyncInsumoDto>> GetInsumosAsync(
        CancellationToken cancellationToken)
    {
        var insumos = await _dbContext.Insumos
            .Include(i => i.PeriodosCarencia)
            .Where(i => i.Activo)
            .ToListAsync(cancellationToken);

        return insumos.Select(i => new SyncInsumoDto
        {
            Id = i.Id,
            NombreComercial = i.NombreComercial,
            IngredienteActivo = i.IngredienteActivo,
            TipoProducto = i.TipoProducto,
            CategoriaToxico = i.CategoriaToxico,
            ConcentracionValor = i.Concentracion.Valor,
            ConcentracionUnidad = i.Concentracion.Unidad,
            DosisMinima = i.DosisMinima,
            DosisMaxima = i.DosisMaxima,
            UnidadDosis = i.UnidadDosis,
            PeriodoCarenciaJson = JsonSerializer.Serialize(
                i.PeriodosCarencia.Select(p => new
                {
                    cultivo = p.Cultivo,
                    diasEspecificos = p.DiasCarencia
                }),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Activo = i.Activo,
            UpdatedAt = i.UpdatedAt
        }).ToList();
    }

    private async Task<IReadOnlyList<SyncAplicacionDto>> GetAplicacionesAsync(
        List<Guid> loteIds, CancellationToken cancellationToken)
    {
        if (loteIds.Count == 0) return [];

        var rows = await _dbContext.Aplicaciones
            .Where(a => loteIds.Contains(a.LoteId))
            .Select(a => new
            {
                a.Id, a.LoteId, a.InsumoId, a.FechaAplicacion,
                DosisCantidad = a.Dosis.Cantidad, DosisUnidad = a.Dosis.Unidad,
                a.AreaAplicadaHa, a.MetodoAplicacion, a.OperadorNombre,
                a.CostoTotal, a.DiasCarenciaAplicables, a.FechaFinCarencia,
                a.CreadoOffline, a.ClientTimestamp, a.RowVersion
            })
            .ToListAsync(cancellationToken);

        return rows.Select(a => new SyncAplicacionDto
        {
            Id = a.Id,
            LoteId = a.LoteId,
            InsumoId = a.InsumoId,
            FechaAplicacion = a.FechaAplicacion,
            DosisCantidad = a.DosisCantidad,
            DosisUnidad = a.DosisUnidad,
            AreaAplicadaHa = a.AreaAplicadaHa,
            MetodoAplicacion = a.MetodoAplicacion,
            OperadorNombre = a.OperadorNombre,
            CostoTotal = a.CostoTotal,
            DiasCarenciaAplicables = a.DiasCarenciaAplicables,
            FechaFinCarencia = a.FechaFinCarencia,
            CreadoOffline = a.CreadoOffline,
            ClientTimestamp = a.ClientTimestamp,
            LoteNombre = "",
            InsumoNombre = "",
            RowVersion = Convert.ToBase64String(a.RowVersion)
        }).ToList();
    }

    private async Task<IReadOnlyList<SyncCosechaDto>> GetCosechasAsync(
        List<Guid> loteIds, CancellationToken cancellationToken)
    {
        if (loteIds.Count == 0) return [];

        var rows = await _dbContext.Cosechas
            .Where(c => loteIds.Contains(c.LoteId))
            .Select(c => new
            {
                c.Id, c.LoteId, c.FechaCosecha, c.PesoTotalKg, c.CalidadGrado,
                c.Comprador, c.PrecioVentaKg, c.IngresoTotal,
                c.BloqueadaPorCarencia, c.CreadoOffline, c.RowVersion
            })
            .ToListAsync(cancellationToken);

        return rows.Select(c => new SyncCosechaDto
        {
            Id = c.Id,
            LoteId = c.LoteId,
            FechaCosecha = c.FechaCosecha,
            PesoTotalKg = c.PesoTotalKg,
            CalidadGrado = c.CalidadGrado,
            Comprador = c.Comprador,
            PrecioVentaKg = c.PrecioVentaKg,
            IngresoTotal = c.IngresoTotal,
            BloqueadaPorCarencia = c.BloqueadaPorCarencia,
            CreadoOffline = c.CreadoOffline,
            RowVersion = Convert.ToBase64String(c.RowVersion)
        }).ToList();
    }

    private async Task<IReadOnlyList<SyncCostoDto>> GetCostosAsync(
        List<Guid> loteIds, CancellationToken cancellationToken)
    {
        if (loteIds.Count == 0) return [];

        // CostosLote has global query filter excluding Eliminado = true.
        var rows = await _dbContext.CostosLote
            .Where(c => loteIds.Contains(c.LoteId))
            .Select(c => new
            {
                c.Id, c.LoteId, c.Fecha, c.Categoria, c.Descripcion,
                c.Monto, c.AplicacionId, c.CosechaId, c.CreadoOffline, c.RowVersion
            })
            .ToListAsync(cancellationToken);

        return rows.Select(c => new SyncCostoDto
        {
            Id = c.Id,
            LoteId = c.LoteId,
            Fecha = c.Fecha,
            Categoria = c.Categoria,
            Descripcion = c.Descripcion,
            Monto = c.Monto,
            AplicacionId = c.AplicacionId,
            CosechaId = c.CosechaId,
            CreadoOffline = c.CreadoOffline,
            RowVersion = Convert.ToBase64String(c.RowVersion)
        }).ToList();
    }
}
