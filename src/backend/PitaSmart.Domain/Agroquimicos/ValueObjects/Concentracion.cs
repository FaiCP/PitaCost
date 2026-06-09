#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Agroquimicos.ValueObjects;

/// <summary>
/// Concentración del principio activo de un insumo agroquímico.
/// </summary>
public class Concentracion : ValueObject
{
    public decimal Valor { get; private set; }
    public string Unidad { get; private set; }

    private Concentracion() { Unidad = string.Empty; }

    public Concentracion(decimal valor, string unidad)
    {
        if (valor < 0) throw new ArgumentException("La concentración no puede ser negativa.", nameof(valor));
        if (string.IsNullOrWhiteSpace(unidad)) throw new ArgumentException("La unidad es requerida.", nameof(unidad));
        Valor = valor;
        Unidad = unidad;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Valor;
        yield return Unidad;
    }
}
