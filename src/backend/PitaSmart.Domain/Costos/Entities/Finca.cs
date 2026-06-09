#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Costos.Entities;

/// <summary>
/// Propiedad agrícola completa de un agricultor.
/// </summary>
public class Finca : BaseEntity, IAuditableEntity
{
    public Guid UsuarioId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Provincia { get; private set; } = string.Empty;
    public string Canton { get; private set; } = string.Empty;
    public string? Parroquia { get; private set; }
    public decimal AreaTotalHa { get; private set; }
    public ICollection<Lote> Lotes { get; private set; } = new List<Lote>();

    private Finca() { }

    /// <summary>Crea una nueva finca asociada a un usuario.</summary>
    public static Finca Crear(
        Guid id,
        Guid usuarioId,
        string nombre,
        string provincia,
        string canton,
        string? parroquia,
        decimal areaTotalHa)
    {
        return new Finca
        {
            Id = id,
            UsuarioId = usuarioId,
            Nombre = nombre,
            Provincia = provincia,
            Canton = canton,
            Parroquia = parroquia,
            AreaTotalHa = areaTotalHa
        };
    }
}
