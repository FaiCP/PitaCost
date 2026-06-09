#nullable enable
namespace PitaSmart.Application.Common.Interfaces;

/// <summary>
/// Proveedor de fecha/hora. Facilita testing al desacoplar del reloj del sistema.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
