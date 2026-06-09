#nullable enable
namespace PitaSmart.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta cosechar un lote con período de carencia activo.
/// Contiene datos del insumo infractor para construir respuestas detalladas al cliente.
/// </summary>
public class PeriodoCarenciaException : DomainException
{
    /// <summary>ID del lote bloqueado.</summary>
    public Guid LoteId { get; }

    /// <summary>ID de la aplicación que origina el bloqueo.</summary>
    public Guid AplicacionId { get; }

    /// <summary>ID del insumo cuyo período de carencia está activo.</summary>
    public Guid InsumoId { get; }

    /// <summary>Nombre comercial del insumo infractor.</summary>
    public string InsumoNombre { get; }

    /// <summary>Fecha en que expira el período de carencia.</summary>
    public DateTimeOffset FechaFinCarencia { get; }

    /// <summary>Días restantes hasta que expire el bloqueo.</summary>
    public int DiasRestantes { get; }

    public PeriodoCarenciaException(
        Guid loteId,
        Guid aplicacionId,
        Guid insumoId,
        string insumoNombre,
        DateTimeOffset fechaFinCarencia,
        int diasRestantes)
        : base(
            "PERIODO_CARENCIA_ACTIVO",
            $"No se puede registrar cosecha. Periodo de carencia activo hasta {fechaFinCarencia:yyyy-MM-dd}. " +
            $"Insumo: {insumoNombre}. Dias restantes: {diasRestantes}.")
    {
        LoteId = loteId;
        AplicacionId = aplicacionId;
        InsumoId = insumoId;
        InsumoNombre = insumoNombre;
        FechaFinCarencia = fechaFinCarencia;
        DiasRestantes = diasRestantes;
    }
}
