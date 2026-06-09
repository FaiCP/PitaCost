#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Costos.Entities;

/// <summary>
/// Registro de costo asociado a un lote de cultivo.
/// </summary>
public class CostoLote : BaseEntity, IAuditableEntity
{
    public Guid LoteId { get; private set; }
    public DateOnly Fecha { get; private set; }

    /// <summary>Categoría: INSUMOS_QUIMICOS, MANO_DE_OBRA, TRANSPORTE, RIEGO, MAQUINARIA, OTROS.</summary>
    public string Categoria { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;
    public decimal Monto { get; private set; }
    public Guid? AplicacionId { get; private set; }
    public Guid? CosechaId { get; private set; }
    public bool CreadoOffline { get; private set; }
    public DateTimeOffset ClientTimestamp { get; private set; }
    public bool Eliminado { get; private set; }
    public DateTimeOffset? EliminadoAt { get; private set; }

    public Lote Lote { get; private set; } = null!;

    private CostoLote() { }

    /// <summary>Crea un nuevo costo de lote.</summary>
    public static CostoLote Crear(
        Guid id,
        Guid loteId,
        DateOnly fecha,
        string categoria,
        string descripcion,
        decimal monto,
        Guid? aplicacionId,
        bool creadoOffline,
        DateTimeOffset clientTimestamp)
    {
        return new CostoLote
        {
            Id = id,
            LoteId = loteId,
            Fecha = fecha,
            Categoria = categoria,
            Descripcion = descripcion,
            Monto = monto,
            AplicacionId = aplicacionId,
            CreadoOffline = creadoOffline,
            ClientTimestamp = clientTimestamp,
            Eliminado = false
        };
    }

    /// <summary>Actualiza los campos editables del costo.</summary>
    public void Actualizar(string descripcion, decimal monto, string categoria, DateOnly fecha)
    {
        Descripcion = descripcion;
        Monto = monto;
        Categoria = categoria;
        Fecha = fecha;
    }

    /// <summary>Marca el costo como eliminado (soft delete).</summary>
    public void SoftDelete()
    {
        Eliminado = true;
        EliminadoAt = DateTimeOffset.UtcNow;
    }
}
