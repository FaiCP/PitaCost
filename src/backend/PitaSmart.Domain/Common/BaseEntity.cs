#nullable enable
using System.ComponentModel.DataAnnotations;

namespace PitaSmart.Domain.Common;

/// <summary>
/// Entidad base con campos de auditoría, control de concurrencia y eventos de dominio.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Identificador único (UUID v4 generado en cliente para soporte offline).</summary>
    public Guid Id { get; protected set; }

    /// <summary>Fecha de creación (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Fecha de última modificación (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Usuario que creó el registro.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Usuario que realizó la última modificación.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Token de concurrencia optimista (SQL Server ROWVERSION).</summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Eventos de dominio pendientes de publicación.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Registra un evento de dominio para su posterior publicación.</summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Limpia todos los eventos de dominio (tras publicación).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
