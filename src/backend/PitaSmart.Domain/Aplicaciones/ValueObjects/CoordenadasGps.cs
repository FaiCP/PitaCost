#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Aplicaciones.ValueObjects;

/// <summary>
/// Coordenadas GPS de una aplicación. Rango válido para Ecuador continental + Galápagos:
/// Latitud [-5, 2], Longitud [-92, -75].
/// </summary>
public class CoordenadasGps : ValueObject
{
    public double Latitud { get; private set; }
    public double Longitud { get; private set; }

    private CoordenadasGps() { }

    public CoordenadasGps(double latitud, double longitud)
    {
        if (latitud is < -5 or > 2)
            throw new ArgumentOutOfRangeException(nameof(latitud), "La latitud debe estar entre -5 y 2 (rango Ecuador).");
        if (longitud is < -92 or > -75)
            throw new ArgumentOutOfRangeException(nameof(longitud), "La longitud debe estar entre -92 y -75 (rango Ecuador).");

        Latitud = latitud;
        Longitud = longitud;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitud;
        yield return Longitud;
    }
}
