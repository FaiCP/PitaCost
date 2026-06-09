#nullable enable
namespace PitaSmart.Domain.Sincronizacion.Enums;

/// <summary>
/// Estado de procesamiento de una operación de sincronización.
/// </summary>
public enum EstadoOperacion
{
    PENDIENTE,
    APLICADA,
    DUPLICADA,
    CONFLICTO,
    RECHAZADA,
    ERROR
}
