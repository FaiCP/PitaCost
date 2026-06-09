#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Aplicaciones.ValueObjects;

/// <summary>
/// Dosis aplicada de un insumo agroquímico. Invariante: Cantidad > 0.
/// </summary>
public class Dosis : ValueObject
{
    /// <summary>Cantidad aplicada.</summary>
    public decimal Cantidad { get; private set; }

    /// <summary>Unidad de medida: L_HA, KG_HA, ML_HA, G_HA, CC_HA.</summary>
    public string Unidad { get; private set; }

    private Dosis() { Unidad = string.Empty; }

    public Dosis(decimal cantidad, string unidad)
    {
        if (cantidad <= 0) throw new ArgumentException("La dosis debe ser mayor a cero.", nameof(cantidad));
        if (string.IsNullOrWhiteSpace(unidad)) throw new ArgumentException("La unidad es requerida.", nameof(unidad));
        Cantidad = cantidad;
        Unidad = unidad;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Cantidad;
        yield return Unidad;
    }
}
