#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Cosecha.Events;

/// <summary>
/// Evento emitido al registrar una cosecha exitosamente.
/// Suscriptores: RegistrarIngresoCosechaHandler (Costos).
/// </summary>
public record CosechaRegistradaEvent(
    Guid CosechaId,
    Guid LoteId,
    DateTimeOffset FechaCosecha,
    decimal PesoTotalKg,
    decimal? PrecioVentaKg,
    decimal? IngresoTotal
) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
