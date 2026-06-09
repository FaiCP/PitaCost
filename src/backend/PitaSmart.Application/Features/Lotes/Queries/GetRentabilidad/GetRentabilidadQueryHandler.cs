#nullable enable
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Aplicaciones.Interfaces;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Application.Features.Lotes.Queries.GetRentabilidad;

/// <summary>
/// Handler para <see cref="GetRentabilidadQuery"/>.
/// Calcula rentabilidad = (totalVentas - totalCostos) con desglose por categoría.
/// Fórmulas según bounded-contexts.md, sección "Cálculo de Rentabilidad".
/// </summary>
public class GetRentabilidadQueryHandler
    : IRequestHandler<GetRentabilidadQuery, ApiResponse<RentabilidadResponse>>
{
    private readonly ILoteRepository _loteRepository;
    private readonly ICostoRepository _costoRepository;
    private readonly IAplicacionRepository _aplicacionRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<GetRentabilidadQueryHandler> _logger;

    public GetRentabilidadQueryHandler(
        ILoteRepository loteRepository,
        ICostoRepository costoRepository,
        IAplicacionRepository aplicacionRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<GetRentabilidadQueryHandler> logger)
    {
        _loteRepository = loteRepository;
        _costoRepository = costoRepository;
        _aplicacionRepository = aplicacionRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<RentabilidadResponse>> Handle(
        GetRentabilidadQuery request,
        CancellationToken cancellationToken)
    {
        var lote = await _loteRepository.GetByIdAsync(request.LoteId, cancellationToken);
        if (lote is null)
        {
            return ApiResponse<RentabilidadResponse>.Fail(
                "LOTE_NO_ENCONTRADO",
                $"El lote con ID {request.LoteId} no existe.");
        }

        var hoy = DateOnly.FromDateTime(_dateTimeProvider.UtcNow.DateTime);
        var desde = request.Desde ?? (lote.FechaInicioSiembra ?? hoy.AddMonths(-6));
        var hasta = request.Hasta ?? hoy;

        // Obtener costos, ingresos y kg cosechados en paralelo.
        var costosTask = _costoRepository.GetCostosByLoteAsync(request.LoteId, desde, hasta, cancellationToken);
        var ingresosTask = _costoRepository.GetIngresosByLoteAsync(request.LoteId, desde, hasta, cancellationToken);
        var totalKgTask = _costoRepository.GetTotalKgCosechadosAsync(request.LoteId, desde, hasta, cancellationToken);
        var carenciaTask = _aplicacionRepository.GetAplicacionesConCarenciaActivaAsync(
            request.LoteId, _dateTimeProvider.UtcNow, cancellationToken);

        await Task.WhenAll(costosTask, ingresosTask, totalKgTask, carenciaTask);

        var costos = await costosTask;
        var ingresos = await ingresosTask;
        var totalKg = await totalKgTask;
        var aplicacionesConCarencia = await carenciaTask;

        // Calcular totales.
        var totalVentas = ingresos.Sum(i => i.TotalVenta);
        var totalCostos = costos.Where(c => !c.Eliminado).Sum(c => c.Monto);
        var utilidadBruta = totalVentas - totalCostos;

        // Desglose por categoría.
        var desglose = costos
            .Where(c => !c.Eliminado)
            .GroupBy(c => c.Categoria)
            .Select(g => new DesgloseCategoriaDto
            {
                Categoria = g.Key,
                Total = g.Sum(c => c.Monto),
                Porcentaje = totalCostos > 0 ? Math.Round(g.Sum(c => c.Monto) / totalCostos * 100, 2) : 0,
                Items = g.Select(c => new ItemCostoDto
                {
                    Descripcion = c.Descripcion,
                    Total = c.Monto
                }).ToList()
            })
            .ToList();

        // Detalle de ventas.
        var detalleVentas = ingresos.Select(i => new DetalleVentaDto
        {
            Fecha = i.Fecha.ToString("yyyy-MM-dd"),
            Comprador = i.Comprador,
            KgVendidos = i.KgVendidos,
            PrecioKg = i.PrecioKg,
            TotalVenta = i.TotalVenta
        }).ToList();

        // Alertas de período de carencia.
        var alertas = aplicacionesConCarencia.Select(a => new AlertaDto
        {
            Tipo = "PERIODO_CARENCIA",
            Mensaje = $"Cosecha bloqueada hasta {a.FechaFinCarencia:yyyy-MM-dd} por aplicacion de insumo.",
            Severidad = "CRITICA"
        }).ToList();

        var precioPromedioKg = totalKg > 0 ? Math.Round(totalVentas / totalKg, 4) : 0;

        var response = new RentabilidadResponse
        {
            LoteId = lote.Id,
            LoteNombre = lote.Nombre,
            CultivoActual = lote.Cultivo,
            AreaHa = lote.AreaHa,
            Periodo = new PeriodoDto(desde.ToString("yyyy-MM-dd"), hasta.ToString("yyyy-MM-dd")),
            Ingresos = new IngresosDto
            {
                TotalVentas = totalVentas,
                PrecioPromedioKg = precioPromedioKg,
                TotalKgCosechados = totalKg,
                DetalleVentas = detalleVentas
            },
            Costos = new CostosDto
            {
                TotalCostos = totalCostos,
                CostoPorHa = lote.AreaHa > 0 ? Math.Round(totalCostos / lote.AreaHa, 2) : 0,
                CostoPorKg = totalKg > 0 ? Math.Round(totalCostos / totalKg, 4) : 0,
                Desglose = desglose
            },
            Rentabilidad = new RentabilidadCalculoDto
            {
                UtilidadBruta = utilidadBruta,
                MargenBruto = totalVentas > 0 ? Math.Round(utilidadBruta / totalVentas * 100, 2) : 0,
                UtilidadPorHa = lote.AreaHa > 0 ? Math.Round(utilidadBruta / lote.AreaHa, 2) : 0,
                UtilidadPorKg = totalKg > 0 ? Math.Round(utilidadBruta / totalKg, 4) : 0,
                Roi = totalCostos > 0 ? Math.Round(utilidadBruta / totalCostos * 100, 2) : 0
            },
            Alertas = alertas
        };

        return ApiResponse<RentabilidadResponse>.Ok(response);
    }
}
