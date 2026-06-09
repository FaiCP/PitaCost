#nullable enable
using Microsoft.EntityFrameworkCore;
using PitaSmart.Domain.Sincronizacion.Entities;
using PitaSmart.Domain.Sincronizacion.Interfaces;

namespace PitaSmart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de operaciones de sincronización.
/// </summary>
public class SyncRepository : ISyncRepository
{
    private readonly PitaSmartDbContext _context;

    public SyncRepository(PitaSmartDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<bool> OperacionYaProcesadaAsync(Guid operacionId, CancellationToken cancellationToken = default)
    {
        return await _context.OperacionesPendientes
            .AnyAsync(o => o.OperacionId == operacionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RegistrarOperacionAsync(OperacionPendiente operacion, CancellationToken cancellationToken = default)
    {
        await _context.OperacionesPendientes.AddAsync(operacion, cancellationToken);
    }
}
