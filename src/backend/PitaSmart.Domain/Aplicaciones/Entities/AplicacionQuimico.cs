#nullable enable
using PitaSmart.Domain.Aplicaciones.Events;
using PitaSmart.Domain.Aplicaciones.ValueObjects;
using PitaSmart.Domain.Common;
using PitaSmart.Domain.Exceptions;

namespace PitaSmart.Domain.Aplicaciones.Entities;

/// <summary>
/// Registro de la aplicación de un insumo agroquímico a un lote.
/// Aggregate Root del bounded context Aplicaciones.
/// Genera trazabilidad regulada por Agrocalidad.
/// </summary>
public class AplicacionQuimico : BaseEntity, IAuditableEntity
{
    /// <summary>FK al lote donde se aplicó el insumo.</summary>
    public Guid LoteId { get; private set; }

    /// <summary>FK al insumo agroquímico del catálogo.</summary>
    public Guid InsumoId { get; private set; }

    /// <summary>Fecha y hora en que se realizó la aplicación en campo.</summary>
    public DateTimeOffset FechaAplicacion { get; private set; }

    /// <summary>Dosis aplicada (cantidad + unidad).</summary>
    public Dosis Dosis { get; private set; } = null!;

    /// <summary>Hectáreas tratadas.</summary>
    public decimal AreaAplicadaHa { get; private set; }

    /// <summary>Método de aplicación: FUMIGACION, DRENCH, INYECCION, GRANULAR, OTRO.</summary>
    public string MetodoAplicacion { get; private set; } = string.Empty;

    /// <summary>Nombre del operador que realizó la aplicación.</summary>
    public string OperadorNombre { get; private set; } = string.Empty;

    /// <summary>Coordenadas GPS del punto de aplicación (nullable).</summary>
    public CoordenadasGps? CoordenadasGps { get; private set; }

    /// <summary>Observaciones del agricultor.</summary>
    public string? Observaciones { get; private set; }

    /// <summary>Costo total de esta aplicación en USD.</summary>
    public decimal CostoTotal { get; private set; }

    /// <summary>Días de carencia aplicables según el insumo y el cultivo del lote.</summary>
    public int DiasCarenciaAplicables { get; private set; }

    /// <summary>Fecha calculada: FechaAplicacion + DiasCarencia.</summary>
    public DateTimeOffset FechaFinCarencia { get; private set; }

    /// <summary>Indica si el registro fue creado sin conexión a Internet.</summary>
    public bool CreadoOffline { get; private set; }

    /// <summary>Timestamp del cliente al momento de crear el registro.</summary>
    public DateTimeOffset ClientTimestamp { get; private set; }

    /// <summary>Identificador del dispositivo de origen.</summary>
    public string? DeviceId { get; private set; }

    private AplicacionQuimico() { } // EF Core

    /// <summary>
    /// Crea una nueva aplicación de químico con validación de dominio.
    /// </summary>
    public static AplicacionQuimico Crear(
        Guid id,
        Guid loteId,
        Guid insumoId,
        DateTimeOffset fechaAplicacion,
        Dosis dosis,
        decimal areaAplicadaHa,
        string metodoAplicacion,
        string operadorNombre,
        CoordenadasGps? coordenadasGps,
        string? observaciones,
        decimal costoTotal,
        int diasCarencia,
        bool creadoOffline,
        DateTimeOffset clientTimestamp,
        string? deviceId)
    {
        var aplicacion = new AplicacionQuimico
        {
            Id = id,
            LoteId = loteId,
            InsumoId = insumoId,
            FechaAplicacion = fechaAplicacion,
            Dosis = dosis,
            AreaAplicadaHa = areaAplicadaHa,
            MetodoAplicacion = metodoAplicacion,
            OperadorNombre = operadorNombre,
            CoordenadasGps = coordenadasGps,
            Observaciones = observaciones,
            CostoTotal = costoTotal,
            DiasCarenciaAplicables = diasCarencia,
            FechaFinCarencia = fechaAplicacion.AddDays(diasCarencia),
            CreadoOffline = creadoOffline,
            ClientTimestamp = clientTimestamp,
            DeviceId = deviceId
        };

        aplicacion.RaiseDomainEvent(new AplicacionRegistradaEvent(
            aplicacion.Id,
            aplicacion.LoteId,
            aplicacion.InsumoId,
            aplicacion.FechaAplicacion,
            aplicacion.DiasCarenciaAplicables,
            aplicacion.FechaFinCarencia,
            aplicacion.CostoTotal));

        return aplicacion;
    }

    /// <summary>
    /// Valida que la dosis no exceda la máxima permitida para el insumo.
    /// Lanza <see cref="DomainException"/> si excede.
    /// </summary>
    /// <param name="dosisMaximaPermitida">Dosis máxima configurada en el catálogo del insumo.</param>
    public void ValidarDosis(decimal dosisMaximaPermitida)
    {
        if (Dosis.Cantidad > dosisMaximaPermitida)
        {
            throw new DomainException(
                "DOSIS_EXCEDIDA",
                $"La dosis {Dosis.Cantidad} {Dosis.Unidad} excede la dosis maxima permitida de {dosisMaximaPermitida} {Dosis.Unidad}.");
        }
    }

    /// <summary>
    /// Determina si esta aplicación tiene un período de carencia activo a la fecha de cosecha indicada.
    /// </summary>
    /// <param name="fechaCosecha">Fecha en que se pretende cosechar.</param>
    /// <param name="diasCarencia">Días de carencia del insumo para el cultivo del lote.</param>
    /// <returns>true si la cosecha está dentro del período de carencia; false si ya expiró.</returns>
    public bool EstaEnPeriodoCarencia(DateTime fechaCosecha, int diasCarencia)
    {
        var finCarencia = FechaAplicacion.AddDays(diasCarencia);
        return fechaCosecha < finCarencia.DateTime;
    }
}
