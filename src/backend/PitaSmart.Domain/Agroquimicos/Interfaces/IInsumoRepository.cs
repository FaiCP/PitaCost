#nullable enable
using PitaSmart.Domain.Agroquimicos.Entities;

namespace PitaSmart.Domain.Agroquimicos.Interfaces;

/// <summary>
/// Repositorio del catálogo de insumos agroquímicos.
/// </summary>
public interface IInsumoRepository
{
    /// <summary>Obtiene un insumo por su ID, incluyendo períodos de carencia.</summary>
    Task<Insumo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Verifica si un insumo existe.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
