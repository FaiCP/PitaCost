#nullable enable
using PitaSmart.Domain.Costos.Entities;

namespace PitaSmart.Domain.Costos.Interfaces;

/// <summary>
/// Repositorio para lotes de cultivo.
/// </summary>
public interface ILoteRepository
{
    Task<Lote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Verifica que el lote pertenezca a una finca del usuario indicado.</summary>
    Task<bool> BelongsToUserAsync(Guid loteId, Guid usuarioId, CancellationToken cancellationToken = default);
}
