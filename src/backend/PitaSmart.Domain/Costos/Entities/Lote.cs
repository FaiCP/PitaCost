#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Costos.Entities;

/// <summary>
/// Subdivisión de una finca dedicada a un cultivo específico.
/// Aggregate Root del bounded context Costos.
/// </summary>
public class Lote : BaseEntity, IAuditableEntity
{
    public Guid FincaId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Cultivo { get; private set; } = string.Empty;
    public decimal AreaHa { get; private set; }
    public double? Latitud { get; private set; }
    public double? Longitud { get; private set; }
    public DateOnly? FechaInicioSiembra { get; private set; }
    public bool Activo { get; private set; } = true;
    public Finca Finca { get; private set; } = null!;
    public ICollection<CostoLote> Costos { get; private set; } = new List<CostoLote>();
    public ICollection<IngresoLote> Ingresos { get; private set; } = new List<IngresoLote>();

    private Lote() { }

    /// <summary>Crea un nuevo lote asociado a una finca.</summary>
    public static Lote Crear(
        Guid id,
        Guid fincaId,
        string nombre,
        string cultivo,
        decimal areaHa,
        double? latitud,
        double? longitud,
        DateOnly? fechaInicioSiembra)
    {
        return new Lote
        {
            Id = id,
            FincaId = fincaId,
            Nombre = nombre,
            Cultivo = cultivo,
            AreaHa = areaHa,
            Latitud = latitud,
            Longitud = longitud,
            FechaInicioSiembra = fechaInicioSiembra,
            Activo = true
        };
    }
}
