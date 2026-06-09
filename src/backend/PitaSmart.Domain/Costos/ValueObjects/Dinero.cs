#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Costos.ValueObjects;

/// <summary>
/// Value Object de monto monetario. Moneda siempre USD (moneda oficial de Ecuador).
/// Invariante: Monto >= 0.
/// </summary>
public class Dinero : ValueObject
{
    public decimal Monto { get; private set; }
    public string Moneda { get; private set; } = "USD";

    private Dinero() { }

    public Dinero(decimal monto)
    {
        if (monto < 0) throw new ArgumentException("El monto no puede ser negativo.", nameof(monto));
        Monto = monto;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Monto;
        yield return Moneda;
    }
}
