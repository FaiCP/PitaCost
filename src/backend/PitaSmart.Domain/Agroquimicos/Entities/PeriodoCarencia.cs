#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Agroquimicos.Entities;

/// <summary>
/// Período de carencia de un insumo para un cultivo específico.
/// Regulado por Agrocalidad.
/// </summary>
public class PeriodoCarencia : BaseEntity
{
    /// <summary>FK al insumo agroquímico.</summary>
    public Guid InsumoId { get; private set; }

    /// <summary>Tipo de cultivo (Banano, Cacao, etc.).</summary>
    public string Cultivo { get; private set; } = string.Empty;

    /// <summary>Días de espera obligatorios antes de cosechar.</summary>
    public int DiasCarencia { get; private set; }

    /// <summary>Referencia normativa de Agrocalidad.</summary>
    public string? FuenteRegulacion { get; private set; }

    /// <summary>Navegación al insumo padre.</summary>
    public Insumo Insumo { get; private set; } = null!;

    private PeriodoCarencia() { } // EF Core
}
