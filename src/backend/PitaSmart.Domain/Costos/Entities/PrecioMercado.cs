#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Costos.Entities;

/// <summary>
/// Precio de mercado de referencia para un cultivo.
/// </summary>
public class PrecioMercado : BaseEntity
{
    public string Cultivo { get; private set; } = string.Empty;
    public decimal PrecioKg { get; private set; }
    public string Fuente { get; private set; } = string.Empty;
    public DateOnly FechaPublicacion { get; private set; }
    public bool Vigente { get; private set; }

    private PrecioMercado() { }
}
