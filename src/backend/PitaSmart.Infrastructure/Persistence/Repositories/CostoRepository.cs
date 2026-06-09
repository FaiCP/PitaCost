#nullable enable
using Microsoft.EntityFrameworkCore;
using PitaSmart.Domain.Costos.Entities;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de costos e ingresos de lotes.
/// </summary>
public class CostoRepository : ICostoRepository
{
    private readonly PitaSmartDbContext _context;

    public CostoRepository(PitaSmartDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CostoLote>> GetCostosByLoteAsync(
        Guid loteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        // El global query filter ya excluye soft-deleted, pero lo explicitamos
        // por claridad en la consulta de rentabilidad.
        return await _context.CostosLote
            .Where(c => c.LoteId == loteId && c.Fecha >= desde && c.Fecha <= hasta)
            .OrderBy(c => c.Fecha)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IngresoLote>> GetIngresosByLoteAsync(
        Guid loteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        return await _context.IngresosLote
            .Where(i => i.LoteId == loteId && i.Fecha >= desde && i.Fecha <= hasta)
            .OrderBy(i => i.Fecha)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<decimal> GetTotalKgCosechadosAsync(
        Guid loteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        return await _context.Cosechas
            .Where(c => c.LoteId == loteId
                && DateOnly.FromDateTime(c.FechaCosecha.DateTime) >= desde
                && DateOnly.FromDateTime(c.FechaCosecha.DateTime) <= hasta)
            .SumAsync(c => c.PesoTotalKg, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddCostoAsync(CostoLote costo, CancellationToken cancellationToken = default)
    {
        await _context.CostosLote.AddAsync(costo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CostosLote.AnyAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CostoLote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CostosLote.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }
}
