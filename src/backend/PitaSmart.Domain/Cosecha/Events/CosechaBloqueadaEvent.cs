#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Cosecha.Events;

/// <summary>
/// Evento emitido cuando se intenta registrar una cosecha que queda bloqueada por período de carencia.
/// Suscriptores: Logging/Auditoría, Notificación al usuario.
/// </summary>
public record CosechaBloqueadaEvent(
    Guid LoteId,
    Guid AplicacionId,
    string InsumoNombre,
    DateTimeOffset FechaFinCarencia,
    int DiasRestantes
) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
