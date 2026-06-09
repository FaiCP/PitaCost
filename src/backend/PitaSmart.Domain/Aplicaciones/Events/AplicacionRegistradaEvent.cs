#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Aplicaciones.Events;

/// <summary>
/// Evento emitido cuando se registra una nueva aplicación de insumo agroquímico.
/// Suscriptores: RecalcularBloqueoHandler (Cosecha), RegistrarCostoAplicacionHandler (Costos).
/// </summary>
public record AplicacionRegistradaEvent(
    Guid AplicacionId,
    Guid LoteId,
    Guid InsumoId,
    DateTimeOffset FechaAplicacion,
    int DiasCarencia,
    DateTimeOffset FechaFinCarencia,
    decimal CostoTotal
) : IDomainEvent
{
    /// <inheritdoc />
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
