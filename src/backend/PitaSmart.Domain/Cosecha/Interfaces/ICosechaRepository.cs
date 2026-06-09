#nullable enable
namespace PitaSmart.Domain.Cosecha.Interfaces;

/// <summary>
/// Repositorio para registros de cosecha.
/// </summary>
public interface ICosechaRepository
{
    Task<Entities.Cosecha?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Entities.Cosecha cosecha, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Obtiene todas las cosechas de un lote, ordenadas por fecha descendente.</summary>
    Task<IReadOnlyList<Entities.Cosecha>> GetByLoteIdAsync(Guid loteId, CancellationToken cancellationToken = default);
}
