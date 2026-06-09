#nullable enable
using PitaSmart.Domain.Costos.Entities;

namespace PitaSmart.Domain.Costos.Interfaces;

/// <summary>
/// Repositorio para costos e ingresos de lotes.
/// </summary>
public interface ICostoRepository
{
    Task<IReadOnlyList<CostoLote>> GetCostosByLoteAsync(
        Guid loteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngresoLote>> GetIngresosByLoteAsync(
        Guid loteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalKgCosechadosAsync(
        Guid loteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default);

    Task AddCostoAsync(CostoLote costo, CancellationToken cancellationToken = default);

    Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CostoLote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
