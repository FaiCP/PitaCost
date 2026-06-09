#nullable enable
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Agroquimicos.Interfaces;
using PitaSmart.Domain.Aplicaciones.Interfaces;
using PitaSmart.Domain.Cosecha.Interfaces;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Application.Features.Cosechas.Commands.RegistrarCosecha;

/// <summary>
/// Handler para <see cref="RegistrarCosechaCommand"/>.
/// Flujo:
///   1. Idempotencia: si ya existe la cosecha, retorna la existente.
///   2. Carga el lote para obtener nombre y cultivo.
///   3. CRITICO: Consulta aplicaciones con carencia activa en el lote.
///      Si alguna tiene FechaFinCarencia > FechaCosecha, lanza PeriodoCarenciaException.
///   4. Crea la entidad Cosecha, calcula IngresoTotal.
///   5. Persiste y retorna respuesta.
/// </summary>
public class RegistrarCosechaCommandHandler
    : IRequestHandler<RegistrarCosechaCommand, ApiResponse<RegistrarCosechaResponse>>
{
    private readonly ICosechaRepository _cosechaRepository;
    private readonly IAplicacionRepository _aplicacionRepository;
    private readonly IInsumoRepository _insumoRepository;
    private readonly ILoteRepository _loteRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<RegistrarCosechaCommandHandler> _logger;

    public RegistrarCosechaCommandHandler(
        ICosechaRepository cosechaRepository,
        IAplicacionRepository aplicacionRepository,
        IInsumoRepository insumoRepository,
        ILoteRepository loteRepository,
        IApplicationDbContext dbContext,
        ILogger<RegistrarCosechaCommandHandler> logger)
    {
        _cosechaRepository = cosechaRepository;
        _aplicacionRepository = aplicacionRepository;
        _insumoRepository = insumoRepository;
        _loteRepository = loteRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<RegistrarCosechaResponse>> Handle(
        RegistrarCosechaCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Idempotencia: si ya existe la cosecha con este ID, retornar sin reprocesar.
        if (await _cosechaRepository.ExistsAsync(request.Id, cancellationToken))
        {
            _logger.LogInformation(
                "Cosecha {CosechaId} ya existe. Retornando como idempotente.", request.Id);

            var existing = (await _cosechaRepository.GetByIdAsync(request.Id, cancellationToken))!;
            var existingLote = await _loteRepository.GetByIdAsync(existing.LoteId, cancellationToken);

            return ApiResponse<RegistrarCosechaResponse>.Ok(BuildResponse(
                existing, existingLote?.Nombre ?? "N/A"));
        }

        // 2. Cargar el lote.
        var lote = await _loteRepository.GetByIdAsync(request.LoteId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Lote {request.LoteId} no encontrado tras validacion.");

        // 3. CRITICO: Validar Periodo de Carencia.
        // Consultar todas las aplicaciones del lote donde FechaFinCarencia > FechaCosecha.
        var aplicacionesConCarencia = await _aplicacionRepository
            .GetAplicacionesConCarenciaActivaAsync(
                request.LoteId, request.FechaCosecha, cancellationToken);

        if (aplicacionesConCarencia.Count > 0)
        {
            // Cargar los insumos de las aplicaciones infractoras para el mensaje de error.
            var insumoIds = aplicacionesConCarencia
                .Select(a => a.InsumoId)
                .Distinct()
                .ToList();

            var insumos = new List<Domain.Agroquimicos.Entities.Insumo>();
            foreach (var insumoId in insumoIds)
            {
                var insumo = await _insumoRepository.GetByIdAsync(insumoId, cancellationToken);
                if (insumo is not null)
                    insumos.Add(insumo);
            }

            // Crear una cosecha temporal para invocar la validacion de dominio
            // que lanzara PeriodoCarenciaException con los datos del insumo infractor.
            var cosechaTemporal = Domain.Cosecha.Entities.Cosecha.CrearParaValidacion(
                request.Id,
                request.LoteId,
                request.FechaCosecha);

            // Esto lanza PeriodoCarenciaException — el GlobalExceptionMiddleware la convierte en 422.
            cosechaTemporal.ValidarBloqueoPorCarencia(aplicacionesConCarencia, insumos);
        }

        // 4. Calcular IngresoTotal si ambos valores estan presentes.
        decimal? ingresoTotal = (request.PesoTotalKg > 0 && request.PrecioVentaKg.HasValue)
            ? request.PesoTotalKg * request.PrecioVentaKg.Value
            : null;

        // 5. Crear entidad de dominio (emite CosechaRegistradaEvent).
        var cosecha = Domain.Cosecha.Entities.Cosecha.Crear(
            id: request.Id,
            loteId: request.LoteId,
            fechaCosecha: request.FechaCosecha,
            pesoTotalKg: request.PesoTotalKg,
            calidadGrado: request.CalidadGrado,
            comprador: request.Comprador,
            precioVentaKg: request.PrecioVentaKg,
            ingresoTotal: ingresoTotal,
            observaciones: request.Observaciones,
            creadoOffline: request.CreadoOffline,
            clientTimestamp: request.ClientTimestamp);

        // 6. Persistir.
        await _cosechaRepository.AddAsync(cosecha, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cosecha {CosechaId} registrada en lote {LoteId} ({LoteNombre}). " +
            "Peso: {PesoKg}kg, Calidad: {Calidad}, Ingreso: {Ingreso}",
            cosecha.Id, cosecha.LoteId, lote.Nombre,
            cosecha.PesoTotalKg, cosecha.CalidadGrado, ingresoTotal);

        return ApiResponse<RegistrarCosechaResponse>.Ok(BuildResponse(cosecha, lote.Nombre));
    }

    private static RegistrarCosechaResponse BuildResponse(
        Domain.Cosecha.Entities.Cosecha cosecha, string loteNombre)
    {
        return new RegistrarCosechaResponse
        {
            Id = cosecha.Id,
            LoteId = cosecha.LoteId,
            LoteNombre = loteNombre,
            FechaCosecha = cosecha.FechaCosecha,
            PesoTotalKg = cosecha.PesoTotalKg,
            CalidadGrado = cosecha.CalidadGrado,
            Comprador = cosecha.Comprador,
            PrecioVentaKg = cosecha.PrecioVentaKg,
            IngresoTotal = cosecha.IngresoTotal,
            BloqueadaPorCarencia = cosecha.BloqueadaPorCarencia,
            RowVersion = Convert.ToBase64String(cosecha.RowVersion),
            CreatedAt = cosecha.CreatedAt
        };
    }
}
