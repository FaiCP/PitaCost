#nullable enable
using Microsoft.EntityFrameworkCore;
using PitaSmart.Domain.Aplicaciones.Entities;
using PitaSmart.Domain.Aplicaciones.Interfaces;

namespace PitaSmart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de aplicaciones de insumos agroquímicos.
/// </summary>
public class AplicacionRepository : IAplicacionRepository
{
    private readonly PitaSmartDbContext _context;

    public AplicacionRepository(PitaSmartDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(AplicacionQuimico aplicacion, CancellationToken cancellationToken = default)
    {
        await _context.Aplicaciones.AddAsync(aplicacion, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AplicacionQuimico?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Aplicaciones
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AplicacionQuimico?> GetUltimaAplicacionAsync(
        Guid loteId, Guid insumoId, CancellationToken cancellationToken = default)
    {
        return await _context.Aplicaciones
            .Where(a => a.LoteId == loteId && a.InsumoId == insumoId)
            .OrderByDescending(a => a.FechaAplicacion)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AplicacionQuimico>> GetAplicacionesConCarenciaActivaAsync(
        Guid loteId, DateTimeOffset fechaReferencia, CancellationToken cancellationToken = default)
    {
        return await _context.Aplicaciones
            .Where(a => a.LoteId == loteId && a.FechaFinCarencia > fechaReferencia)
            .OrderByDescending(a => a.FechaFinCarencia)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Aplicaciones.AnyAsync(a => a.Id == id, cancellationToken);
    }
}
