#nullable enable
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Application.Features.Aplicaciones.Commands.RegistrarAplicacion;
using PitaSmart.Domain.Exceptions;
using PitaSmart.Domain.Sincronizacion.Entities;
using PitaSmart.Domain.Sincronizacion.Enums;
using PitaSmart.Domain.Sincronizacion.Interfaces;

namespace PitaSmart.Application.Features.Sync.Commands.ProcessSyncBatch;

/// <summary>
/// Handler para <see cref="ProcessSyncBatchCommand"/>.
/// Procesa cada operación en orden, maneja idempotencia, conflictos RowVersion,
/// y errores de validación de negocio. Cada operación se procesa independientemente.
/// </summary>
public class ProcessSyncBatchCommandHandler
    : IRequestHandler<ProcessSyncBatchCommand, ApiResponse<SyncBatchResult>>
{
    private readonly ISyncRepository _syncRepository;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<ProcessSyncBatchCommandHandler> _logger;

    public ProcessSyncBatchCommandHandler(
        ISyncRepository syncRepository,
        IMediator mediator,
        ICurrentUserService currentUser,
        IApplicationDbContext dbContext,
        ILogger<ProcessSyncBatchCommandHandler> logger)
    {
        _syncRepository = syncRepository;
        _mediator = mediator;
        _currentUser = currentUser;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<SyncBatchResult>> Handle(
        ProcessSyncBatchCommand request,
        CancellationToken cancellationToken)
    {
        var resultados = new List<SyncOperacionResult>();

        // Procesar operaciones en orden de clientTimestamp.
        var operacionesOrdenadas = request.Operaciones
            .OrderBy(o => o.ClientTimestamp)
            .ToList();

        foreach (var operacion in operacionesOrdenadas)
        {
            var resultado = await ProcesarOperacionAsync(operacion, request.DeviceId, cancellationToken);
            resultados.Add(resultado);
        }

        var batchResult = new SyncBatchResult
        {
            DeviceId = request.DeviceId,
            ServerTimestamp = DateTimeOffset.UtcNow,
            Resultados = resultados
        };

        return ApiResponse<SyncBatchResult>.Ok(batchResult);
    }

    private async Task<SyncOperacionResult> ProcesarOperacionAsync(
        OperacionOfflineDto operacion,
        string deviceId,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Verificar idempotencia.
            if (await _syncRepository.OperacionYaProcesadaAsync(operacion.OperacionId, cancellationToken))
            {
                _logger.LogInformation(
                    "Operacion {OperacionId} ya fue procesada. Retornando DUPLICADA.", operacion.OperacionId);

                return new SyncOperacionResult
                {
                    OperacionId = operacion.OperacionId,
                    Estado = "DUPLICADA",
                    EntidadId = operacion.EntidadId
                };
            }

            // 2. Despachar al handler correspondiente según tipo de operación.
            var resultado = await DespacharOperacionAsync(operacion, cancellationToken);

            // 3. Registrar operación procesada para idempotencia.
            await _syncRepository.RegistrarOperacionAsync(
                OperacionPendiente.Crear(
                    id: Guid.NewGuid(),
                    operacionId: operacion.OperacionId,
                    deviceId: deviceId,
                    usuarioId: _currentUser.UserId,
                    tipo: Enum.Parse<TipoOperacion>(operacion.Tipo),
                    entidadId: operacion.EntidadId,
                    entidadTipo: operacion.EntidadTipo,
                    payload: operacion.Payload.RootElement.GetRawText(),
                    clientTimestamp: operacion.ClientTimestamp,
                    rowVersionAnterior: operacion.RowVersionAnterior is not null
                        ? Convert.FromBase64String(operacion.RowVersionAnterior)
                        : null,
                    estado: Enum.Parse<EstadoOperacion>(resultado.Estado),
                    intentoNumero: operacion.IntentoNumero,
                    procesadoAt: DateTimeOffset.UtcNow),
                cancellationToken);

            // CancellationToken.None: la escritura debe completarse aunque el cliente se desconecte.
            // EnableRetryOnFailure lanza OperationCanceledException si el token se cancela durante un retry.
            await _dbContext.SaveChangesAsync(CancellationToken.None);

            return resultado;
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex,
                "Operacion {OperacionId} rechazada por regla de negocio: {Code}",
                operacion.OperacionId, ex.Code);

            return new SyncOperacionResult
            {
                OperacionId = operacion.OperacionId,
                Estado = "RECHAZADA",
                EntidadId = operacion.EntidadId,
                Error = new SyncOperacionError
                {
                    Code = ex.Code,
                    Message = ex.Message
                }
            };
        }
        catch (FluentValidation.ValidationException ex)
        {
            _logger.LogWarning(
                "Operacion {OperacionId} rechazada por validacion: {Errors}",
                operacion.OperacionId,
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));

            return new SyncOperacionResult
            {
                OperacionId = operacion.OperacionId,
                Estado = "RECHAZADA",
                EntidadId = operacion.EntidadId,
                Error = new SyncOperacionError
                {
                    Code = "VALIDATION_ERROR",
                    Message = string.Join("; ", ex.Errors.Select(e => e.ErrorMessage))
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error inesperado procesando operacion {OperacionId}", operacion.OperacionId);

            return new SyncOperacionResult
            {
                OperacionId = operacion.OperacionId,
                Estado = "ERROR",
                EntidadId = operacion.EntidadId,
                Error = new SyncOperacionError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "Error interno procesando la operacion."
                }
            };
        }
    }

    /// <summary>
    /// Despacha la operación al handler MediatR correspondiente según su tipo.
    /// Actúa como Anti-Corruption Layer: el contexto de Sync no contiene lógica de negocio.
    /// </summary>
    private async Task<SyncOperacionResult> DespacharOperacionAsync(
        OperacionOfflineDto operacion,
        CancellationToken cancellationToken)
    {
        return operacion.Tipo switch
        {
            "CREAR_APLICACION" => await ProcesarCrearAplicacionAsync(operacion, cancellationToken),
            // Otros tipos se implementarían aquí siguiendo el mismo patrón:
            // "CREAR_COSECHA" => ...
            // "CREAR_COSTO" => ...
            // "ACTUALIZAR_COSTO" => ...
            // "ELIMINAR_COSTO" => ...
            _ => new SyncOperacionResult
            {
                OperacionId = operacion.OperacionId,
                Estado = "RECHAZADA",
                EntidadId = operacion.EntidadId,
                Error = new SyncOperacionError
                {
                    Code = "TIPO_OPERACION_NO_SOPORTADO",
                    Message = $"El tipo de operacion '{operacion.Tipo}' no esta soportado."
                }
            }
        };
    }

    private async Task<SyncOperacionResult> ProcesarCrearAplicacionAsync(
        OperacionOfflineDto operacion,
        CancellationToken cancellationToken)
    {
        var payload = operacion.Payload.RootElement;

        var command = new RegistrarAplicacionCommand
        {
            Id = operacion.EntidadId,
            LoteId = payload.GetProperty("loteId").GetGuid(),
            InsumoId = payload.GetProperty("insumoId").GetGuid(),
            FechaAplicacion = payload.GetProperty("fechaAplicacion").GetDateTimeOffset(),
            Dosis = new DosisDto
            {
                Cantidad = payload.GetProperty("dosis").GetProperty("cantidad").GetDecimal(),
                Unidad = payload.GetProperty("dosis").GetProperty("unidad").GetString()!
            },
            AreaAplicadaHa = payload.GetProperty("areaAplicadaHa").GetDecimal(),
            MetodoAplicacion = payload.GetProperty("metodoAplicacion").GetString()!,
            OperadorNombre = payload.GetProperty("operadorNombre").GetString()!,
            CostoTotal = payload.TryGetProperty("costoTotal", out var costo) ? costo.GetDecimal() : 0,
            CreadoOffline = true,
            ClientTimestamp = operacion.ClientTimestamp
        };

        var response = await _mediator.Send(command, cancellationToken);

        if (!response.Success)
        {
            return new SyncOperacionResult
            {
                OperacionId = operacion.OperacionId,
                Estado = "RECHAZADA",
                EntidadId = operacion.EntidadId,
                Error = new SyncOperacionError
                {
                    Code = response.Error?.Code ?? "UNKNOWN",
                    Message = response.Error?.Message ?? "Error desconocido."
                }
            };
        }

        return new SyncOperacionResult
        {
            OperacionId = operacion.OperacionId,
            Estado = "APLICADA",
            EntidadId = operacion.EntidadId,
            RowVersionNuevo = response.Data?.RowVersion
        };
    }
}
