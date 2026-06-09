#nullable enable
using PitaSmart.Application.Common.Interfaces;

namespace PitaSmart.Infrastructure.Identity;

/// <summary>
/// Proveedor de fecha/hora del sistema. Facilita testing por inyección.
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
