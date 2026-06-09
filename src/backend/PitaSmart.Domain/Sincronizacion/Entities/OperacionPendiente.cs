#nullable enable
using PitaSmart.Domain.Common;
using PitaSmart.Domain.Sincronizacion.Enums;

namespace PitaSmart.Domain.Sincronizacion.Entities;

/// <summary>
/// Registro de una operación de sincronización offline recibida del dispositivo.
/// </summary>
public class OperacionPendiente : BaseEntity
{
    /// <summary>Constructor requerido por EF Core.</summary>
    private OperacionPendiente() { }

    /// <summary>Crea un nuevo registro de operación procesada.</summary>
    public static OperacionPendiente Crear(
        Guid id,
        Guid operacionId,
        string deviceId,
        Guid usuarioId,
        TipoOperacion tipo,
        Guid entidadId,
        string entidadTipo,
        string payload,
        DateTimeOffset clientTimestamp,
        byte[]? rowVersionAnterior,
        EstadoOperacion estado,
        int intentoNumero,
        DateTimeOffset procesadoAt)
    {
        return new OperacionPendiente
        {
            Id = id,
            OperacionId = operacionId,
            DeviceId = deviceId,
            UsuarioId = usuarioId,
            Tipo = tipo,
            EntidadId = entidadId,
            EntidadTipo = entidadTipo,
            Payload = payload,
            ClientTimestamp = clientTimestamp,
            RowVersionAnterior = rowVersionAnterior,
            Estado = estado,
            IntentoNumero = intentoNumero,
            ProcesadoAt = procesadoAt
        };
    }

    public Guid OperacionId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public Guid UsuarioId { get; set; }
    public TipoOperacion Tipo { get; set; }
    public Guid EntidadId { get; set; }
    public string EntidadTipo { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset ClientTimestamp { get; set; }
    public byte[]? RowVersionAnterior { get; set; }
    public EstadoOperacion Estado { get; set; } = EstadoOperacion.PENDIENTE;
    public int IntentoNumero { get; set; }
    public DateTimeOffset? ProcesadoAt { get; set; }
    public string? ErrorDetalle { get; set; }
}
