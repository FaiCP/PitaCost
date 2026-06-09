#nullable enable
using Microsoft.EntityFrameworkCore;
using PitaSmart.Domain.Cosecha.Interfaces;

namespace PitaSmart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementacion del repositorio de cosechas usando EF Core.
/// </summary>
public class CosechaRepository : ICosechaRepository
{
    private readonly PitaSmartDbContext _dbContext;

    public CosechaRepository(PitaSmartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Domain.Cosecha.Entities.Cosecha?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Cosechas
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(
        Domain.Cosecha.Entities.Cosecha cosecha, CancellationToken cancellationToken = default)
    {
        await _dbContext.Cosechas.AddAsync(cosecha, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Cosechas.AnyAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Cosecha.Entities.Cosecha>> GetByLoteIdAsync(
        Guid loteId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Cosechas
            .Where(c => c.LoteId == loteId)
            .OrderByDescending(c => c.FechaCosecha)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
