#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Costos.Entities;

/// <summary>
/// Ingreso por venta de cosecha de un lote.
/// </summary>
public class IngresoLote : BaseEntity, IAuditableEntity
{
    public Guid LoteId { get; private set; }
    public Guid CosechaId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public string Comprador { get; private set; } = string.Empty;
    public decimal KgVendidos { get; private set; }
    public decimal PrecioKg { get; private set; }

    /// <summary>Calculado: KgVendidos * PrecioKg.</summary>
    public decimal TotalVenta { get; private set; }

    public Lote Lote { get; private set; } = null!;

    private IngresoLote() { }
}
