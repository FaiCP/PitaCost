#nullable enable
using Microsoft.EntityFrameworkCore;
using PitaSmart.Domain.Agroquimicos.Entities;
using PitaSmart.Domain.Agroquimicos.Interfaces;

namespace PitaSmart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de insumos agroquímicos.
/// </summary>
public class InsumoRepository : IInsumoRepository
{
    private readonly PitaSmartDbContext _context;

    public InsumoRepository(PitaSmartDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Insumo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Insumos
            .Include(i => i.PeriodosCarencia)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Insumos.AnyAsync(i => i.Id == id && i.Activo, cancellationToken);
    }
}
