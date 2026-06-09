#nullable enable
using PitaSmart.Domain.Sincronizacion.Entities;

namespace PitaSmart.Domain.Sincronizacion.Interfaces;

/// <summary>
/// Repositorio para operaciones de sincronización y log de idempotencia.
/// </summary>
public interface ISyncRepository
{
    /// <summary>Verifica si una operación ya fue procesada (idempotencia).</summary>
    Task<bool> OperacionYaProcesadaAsync(Guid operacionId, CancellationToken cancellationToken = default);

    /// <summary>Registra una operación procesada en el log de idempotencia.</summary>
    Task RegistrarOperacionAsync(OperacionPendiente operacion, CancellationToken cancellationToken = default);
}
