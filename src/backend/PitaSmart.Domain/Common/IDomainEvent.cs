#nullable enable
namespace PitaSmart.Domain.Common;

/// <summary>
/// Marcador para eventos de dominio que se publican tras persistir el aggregate.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Momento en que se produjo el evento.
    /// </summary>
    DateTimeOffset OccurredOn { get; }
}
