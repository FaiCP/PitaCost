#nullable enable
namespace PitaSmart.Domain.Sincronizacion.Enums;

/// <summary>
/// Tipos de operación soportados en sincronización offline.
/// </summary>
public enum TipoOperacion
{
    CREAR_APLICACION,
    ACTUALIZAR_APLICACION,
    CREAR_COSECHA,
    CREAR_COSTO,
    ACTUALIZAR_COSTO,
    ELIMINAR_COSTO,
    CREAR_LOTE,
    ACTUALIZAR_LOTE
}
