#nullable enable
namespace PitaSmart.Domain.Common;

/// <summary>
/// Marcador para entidades que requieren campos de auditoría automáticos.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    string? CreatedBy { get; set; }
    string? UpdatedBy { get; set; }
}
