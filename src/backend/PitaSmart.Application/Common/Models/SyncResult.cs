#nullable enable
using System.Text.Json;

namespace PitaSmart.Application.Common.Models;

/// <summary>
/// Resultado del procesamiento de un batch de sincronización.
/// </summary>
public record SyncBatchResult
{
    public string DeviceId { get; init; } = string.Empty;
    public DateTimeOffset ServerTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public List<SyncOperacionResult> Resultados { get; init; } = [];
}

/// <summary>
/// Resultado individual de una operación dentro del batch de sync.
/// </summary>
public record SyncOperacionResult
{
    public Guid OperacionId { get; init; }

    /// <summary>APLICADA, DUPLICADA, CONFLICTO, RECHAZADA, ERROR.</summary>
    public string Estado { get; init; } = string.Empty;

    public Guid EntidadId { get; init; }
    public string? RowVersionNuevo { get; init; }
    public SyncOperacionError? Error { get; init; }
}

/// <summary>
/// Detalle de error de una operación de sync.
/// </summary>
public record SyncOperacionError
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    /// <summary>Datos actuales del servidor en caso de conflicto (JSON serializado).</summary>
    public JsonDocument? ServerData { get; init; }
}
