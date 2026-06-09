#nullable enable
using PitaSmart.Domain.Agroquimicos.ValueObjects;
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Agroquimicos.Entities;

/// <summary>
/// Producto agroquímico del catálogo con su ficha técnica y períodos de carencia por cultivo.
/// Aggregate Root del bounded context Agroquímicos.
/// </summary>
public class Insumo : BaseEntity, IAuditableEntity
{
    /// <summary>Nombre comercial del producto.</summary>
    public string NombreComercial { get; private set; } = string.Empty;

    /// <summary>Principio activo químico.</summary>
    public string IngredienteActivo { get; private set; } = string.Empty;

    /// <summary>Empresa fabricante.</summary>
    public string Fabricante { get; private set; } = string.Empty;

    /// <summary>Número de registro oficial ante Agrocalidad.</summary>
    public string RegistroAgrocalidad { get; private set; } = string.Empty;

    /// <summary>Tipo de producto: FUNGICIDA, HERBICIDA, INSECTICIDA, FERTILIZANTE, NEMATICIDA, OTRO.</summary>
    public string TipoProducto { get; private set; } = string.Empty;

    /// <summary>Categoría toxicológica: I, II, III, IV.</summary>
    public string CategoriaToxico { get; private set; } = string.Empty;

    /// <summary>Concentración del principio activo.</summary>
    public Concentracion Concentracion { get; private set; } = null!;

    /// <summary>Dosis mínima recomendada.</summary>
    public decimal DosisMinima { get; private set; }

    /// <summary>Dosis máxima permitida.</summary>
    public decimal DosisMaxima { get; private set; }

    /// <summary>Unidad de medida de la dosis: L_HA, KG_HA, ML_HA, G_HA, CC_HA.</summary>
    public string UnidadDosis { get; private set; } = string.Empty;

    /// <summary>Si está disponible en el catálogo.</summary>
    public bool Activo { get; private set; } = true;

    /// <summary>Períodos de carencia por cultivo.</summary>
    public ICollection<PeriodoCarencia> PeriodosCarencia { get; private set; } = new List<PeriodoCarencia>();

    /// <summary>Fichas técnicas asociadas.</summary>
    public ICollection<FichaTecnica> FichasTecnicas { get; private set; } = new List<FichaTecnica>();

    private Insumo() { } // EF Core

    /// <summary>
    /// Obtiene el período de carencia (días) para un cultivo específico.
    /// Si no hay uno específico definido, retorna el período genérico (el primero disponible).
    /// </summary>
    public int ObtenerDiasCarencia(string cultivo)
    {
        var periodo = PeriodosCarencia.FirstOrDefault(p =>
            p.Cultivo.Equals(cultivo, StringComparison.OrdinalIgnoreCase));

        return periodo?.DiasCarencia
               ?? PeriodosCarencia.FirstOrDefault()?.DiasCarencia
               ?? 0;
    }
}
