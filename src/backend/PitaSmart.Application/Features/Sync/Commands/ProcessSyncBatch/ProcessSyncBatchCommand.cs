#nullable enable
using System.Text.Json;
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Sync.Commands.ProcessSyncBatch;

/// <summary>
/// Comando para procesar un batch de operaciones offline (POST /v1/api/sync/push).
/// </summary>
public record ProcessSyncBatchCommand : IRequest<ApiResponse<SyncBatchResult>>
{
    /// <summary>Identificador único del dispositivo.</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>Última sincronización exitosa del dispositivo.</summary>
    public DateTimeOffset LastSyncTimestamp { get; init; }

    /// <summary>Operaciones pendientes (mín 1, máx 100).</summary>
    public List<OperacionOfflineDto> Operaciones { get; init; } = [];
}

/// <summary>
/// DTO de una operación individual dentro del batch de sincronización.
/// </summary>
public record OperacionOfflineDto
{
    /// <summary>Idempotency key (UUID generado en cliente).</summary>
    public Guid OperacionId { get; init; }

    /// <summary>Tipo de operación: CREAR_APLICACION, ACTUALIZAR_COSTO, etc.</summary>
    public string Tipo { get; init; } = string.Empty;

    /// <summary>ID de la entidad afectada.</summary>
    public Guid EntidadId { get; init; }

    /// <summary>Nombre del tipo de entidad (AplicacionQuimico, CostoLote, etc.).</summary>
    public string EntidadTipo { get; init; } = string.Empty;

    /// <summary>Datos específicos de la operación.</summary>
    public JsonDocument Payload { get; init; } = null!;

    /// <summary>Timestamp del cliente al crear la operación.</summary>
    public DateTimeOffset ClientTimestamp { get; init; }

    /// <summary>RowVersion conocido por el cliente (requerido para updates/deletes).</summary>
    public string? RowVersionAnterior { get; init; }

    /// <summary>Número de intento de envío (1-based).</summary>
    public int IntentoNumero { get; init; }
}
