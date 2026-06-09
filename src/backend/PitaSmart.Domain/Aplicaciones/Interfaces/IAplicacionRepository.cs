#nullable enable
using PitaSmart.Domain.Aplicaciones.Entities;

namespace PitaSmart.Domain.Aplicaciones.Interfaces;

/// <summary>
/// Repositorio para aplicaciones de insumos agroquímicos.
/// </summary>
public interface IAplicacionRepository
{
    /// <summary>Persiste una nueva aplicación.</summary>
    Task AddAsync(AplicacionQuimico aplicacion, CancellationToken cancellationToken = default);

    /// <summary>Obtiene una aplicación por su ID.</summary>
    Task<AplicacionQuimico?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Obtiene la última aplicación de un insumo en un lote.</summary>
    Task<AplicacionQuimico?> GetUltimaAplicacionAsync(Guid loteId, Guid insumoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene todas las aplicaciones de un lote que tienen período de carencia activo
    /// posterior a la fecha indicada.
    /// </summary>
    Task<IReadOnlyList<AplicacionQuimico>> GetAplicacionesConCarenciaActivaAsync(
        Guid loteId,
        DateTimeOffset fechaReferencia,
        CancellationToken cancellationToken = default);

    /// <summary>Verifica si ya existe una aplicación con el ID indicado (idempotencia).</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
