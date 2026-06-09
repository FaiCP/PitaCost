#nullable enable
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Agroquimicos.Interfaces;
using PitaSmart.Domain.Aplicaciones.Entities;
using PitaSmart.Domain.Aplicaciones.Interfaces;
using PitaSmart.Domain.Aplicaciones.ValueObjects;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Application.Features.Aplicaciones.Commands.RegistrarAplicacion;

/// <summary>
/// Handler para <see cref="RegistrarAplicacionCommand"/>.
/// Flujo:
///   1. Idempotencia: si ya existe la aplicación, retorna la existente.
///   2. Carga insumo y lote para obtener dosis máxima y cultivo.
///   3. Calcula días de carencia según insumo + cultivo del lote.
///   4. Crea la entidad AplicacionQuimico (que emite AplicacionRegistradaEvent).
///   5. Valida dosis contra máximo del insumo.
///   6. Persiste y retorna respuesta.
/// </summary>
public class RegistrarAplicacionCommandHandler
    : IRequestHandler<RegistrarAplicacionCommand, ApiResponse<RegistrarAplicacionResponse>>
{
    private readonly IAplicacionRepository _aplicacionRepository;
    private readonly IInsumoRepository _insumoRepository;
    private readonly ILoteRepository _loteRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<RegistrarAplicacionCommandHandler> _logger;

    public RegistrarAplicacionCommandHandler(
        IAplicacionRepository aplicacionRepository,
        IInsumoRepository insumoRepository,
        ILoteRepository loteRepository,
        IApplicationDbContext dbContext,
        ILogger<RegistrarAplicacionCommandHandler> logger)
    {
        _aplicacionRepository = aplicacionRepository;
        _insumoRepository = insumoRepository;
        _loteRepository = loteRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<RegistrarAplicacionResponse>> Handle(
        RegistrarAplicacionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Idempotencia: si ya existe la aplicación con este ID, retornar sin reprocesar.
        if (await _aplicacionRepository.ExistsAsync(request.Id, cancellationToken))
        {
            _logger.LogInformation(
                "Aplicacion {AplicacionId} ya existe. Retornando como idempotente.", request.Id);

            var existing = (await _aplicacionRepository.GetByIdAsync(request.Id, cancellationToken))!;
            var existingInsumo = await _insumoRepository.GetByIdAsync(existing.InsumoId, cancellationToken);
            var existingLote = await _loteRepository.GetByIdAsync(existing.LoteId, cancellationToken);

            return ApiResponse<RegistrarAplicacionResponse>.Ok(BuildResponse(
                existing,
                existingInsumo?.NombreComercial ?? "N/A",
                existingLote?.Nombre ?? "N/A"));
        }

        // 2. Cargar insumo (con períodos de carencia) y lote.
        var insumo = await _insumoRepository.GetByIdAsync(request.InsumoId, cancellationToken)
            ?? throw new InvalidOperationException($"Insumo {request.InsumoId} no encontrado tras validacion.");

        var lote = await _loteRepository.GetByIdAsync(request.LoteId, cancellationToken)
            ?? throw new InvalidOperationException($"Lote {request.LoteId} no encontrado tras validacion.");

        // 3. Calcular días de carencia para el cultivo del lote.
        var diasCarencia = insumo.ObtenerDiasCarencia(lote.Cultivo);

        // 4. Crear Value Objects y entidad de dominio.
        var dosis = new Dosis(request.Dosis.Cantidad, request.Dosis.Unidad);

        CoordenadasGps? coordenadas = request.CoordenadasGps is not null
            ? new CoordenadasGps(request.CoordenadasGps.Latitud, request.CoordenadasGps.Longitud)
            : null;

        var aplicacion = AplicacionQuimico.Crear(
            id: request.Id,
            loteId: request.LoteId,
            insumoId: request.InsumoId,
            fechaAplicacion: request.FechaAplicacion,
            dosis: dosis,
            areaAplicadaHa: request.AreaAplicadaHa,
            metodoAplicacion: request.MetodoAplicacion,
            operadorNombre: request.OperadorNombre,
            coordenadasGps: coordenadas,
            observaciones: request.Observaciones,
            costoTotal: request.CostoTotal,
            diasCarencia: diasCarencia,
            creadoOffline: request.CreadoOffline,
            clientTimestamp: request.ClientTimestamp,
            deviceId: null);

        // 5. Validar dosis contra máximo del insumo (lanza DomainException si excede).
        aplicacion.ValidarDosis(insumo.DosisMaxima);

        // 6. Persistir.
        // CancellationToken.None: la escritura debe completarse aunque el cliente se desconecte.
        // EnableRetryOnFailure lanza OperationCanceledException si el token se cancela durante un retry.
        await _aplicacionRepository.AddAsync(aplicacion, cancellationToken);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation(
            "Aplicacion {AplicacionId} registrada en lote {LoteId}. " +
            "Periodo de carencia: {DiasCarencia} dias, fin: {FechaFinCarencia}",
            aplicacion.Id, aplicacion.LoteId,
            aplicacion.DiasCarenciaAplicables, aplicacion.FechaFinCarencia);

        return ApiResponse<RegistrarAplicacionResponse>.Ok(
            BuildResponse(aplicacion, insumo.NombreComercial, lote.Nombre));
    }

    private static RegistrarAplicacionResponse BuildResponse(
        AplicacionQuimico aplicacion, string insumoNombre, string loteNombre)
    {
        return new RegistrarAplicacionResponse
        {
            Id = aplicacion.Id,
            LoteId = aplicacion.LoteId,
            LoteNombre = loteNombre,
            InsumoId = aplicacion.InsumoId,
            InsumoNombre = insumoNombre,
            FechaAplicacion = aplicacion.FechaAplicacion,
            Dosis = new DosisDto
            {
                Cantidad = aplicacion.Dosis.Cantidad,
                Unidad = aplicacion.Dosis.Unidad
            },
            AreaAplicadaHa = aplicacion.AreaAplicadaHa,
            MetodoAplicacion = aplicacion.MetodoAplicacion,
            OperadorNombre = aplicacion.OperadorNombre,
            CostoTotal = aplicacion.CostoTotal,
            PeriodoCarencia = new PeriodoCarenciaResponseDto
            {
                DiasCarencia = aplicacion.DiasCarenciaAplicables,
                FechaFinCarencia = aplicacion.FechaFinCarencia,
                CosechaBloqueada = aplicacion.FechaFinCarencia > DateTimeOffset.UtcNow
            },
            RowVersion = Convert.ToBase64String(aplicacion.RowVersion),
            CreatedAt = aplicacion.CreatedAt
        };
    }
}
