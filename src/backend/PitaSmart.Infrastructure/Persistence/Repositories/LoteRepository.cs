#nullable enable
using Microsoft.EntityFrameworkCore;
using PitaSmart.Domain.Costos.Entities;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de lotes.
/// </summary>
public class LoteRepository : ILoteRepository
{
    private readonly PitaSmartDbContext _context;

    public LoteRepository(PitaSmartDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Lote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Lotes
            .Include(l => l.Finca)
            .FirstOrDefaultAsync(l => l.Id == id && l.Activo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Lotes.AnyAsync(l => l.Id == id && l.Activo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> BelongsToUserAsync(Guid loteId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        return await _context.Lotes
            .Include(l => l.Finca)
            .AnyAsync(l => l.Id == loteId && l.Finca.UsuarioId == usuarioId, cancellationToken);
    }
}
