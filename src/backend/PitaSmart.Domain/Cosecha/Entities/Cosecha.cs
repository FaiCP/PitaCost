#nullable enable
using PitaSmart.Domain.Agroquimicos.Entities;
using PitaSmart.Domain.Aplicaciones.Entities;
using PitaSmart.Domain.Common;
using PitaSmart.Domain.Exceptions;

namespace PitaSmart.Domain.Cosecha.Entities;

/// <summary>
/// Registro de cosecha de un lote. Aggregate Root del bounded context Cosecha.
/// Contiene la regla de negocio más crítica: bloqueo por período de carencia.
/// </summary>
public class Cosecha : BaseEntity, IAuditableEntity
{
    /// <summary>FK al lote cosechado.</summary>
    public Guid LoteId { get; private set; }

    /// <summary>Fecha de la cosecha.</summary>
    public DateTimeOffset FechaCosecha { get; private set; }

    /// <summary>Peso total cosechado en kilogramos.</summary>
    public decimal PesoTotalKg { get; private set; }

    /// <summary>Grado de calidad: PREMIUM, PRIMERA, SEGUNDA, RECHAZO.</summary>
    public string CalidadGrado { get; private set; } = string.Empty;

    /// <summary>Nombre del comprador (nullable).</summary>
    public string? Comprador { get; private set; }

    /// <summary>Precio de venta por kilogramo (nullable).</summary>
    public decimal? PrecioVentaKg { get; private set; }

    /// <summary>Ingreso total calculado: PesoTotalKg * PrecioVentaKg.</summary>
    public decimal? IngresoTotal { get; private set; }

    /// <summary>Observaciones del agricultor.</summary>
    public string? Observaciones { get; private set; }

    /// <summary>Flag calculado: true si hay carencia activa al momento del registro.</summary>
    public bool BloqueadaPorCarencia { get; private set; }

    /// <summary>Si fue creado sin conexión.</summary>
    public bool CreadoOffline { get; private set; }

    /// <summary>Timestamp del cliente.</summary>
    public DateTimeOffset ClientTimestamp { get; private set; }

    private Cosecha() { } // EF Core

    /// <summary>
    /// Factory method para crear una cosecha completa con todos sus datos.
    /// Emite <see cref="Events.CosechaRegistradaEvent"/> al crearse.
    /// </summary>
    public static Cosecha Crear(
        Guid id,
        Guid loteId,
        DateTimeOffset fechaCosecha,
        decimal pesoTotalKg,
        string calidadGrado,
        string? comprador,
        decimal? precioVentaKg,
        decimal? ingresoTotal,
        string? observaciones,
        bool creadoOffline,
        DateTimeOffset clientTimestamp)
    {
        var cosecha = new Cosecha
        {
            Id = id,
            LoteId = loteId,
            FechaCosecha = fechaCosecha,
            PesoTotalKg = pesoTotalKg,
            CalidadGrado = calidadGrado,
            Comprador = comprador,
            PrecioVentaKg = precioVentaKg,
            IngresoTotal = ingresoTotal,
            Observaciones = observaciones,
            BloqueadaPorCarencia = false,
            CreadoOffline = creadoOffline,
            ClientTimestamp = clientTimestamp
        };

        cosecha.RaiseDomainEvent(new Events.CosechaRegistradaEvent(
            cosecha.Id,
            cosecha.LoteId,
            cosecha.FechaCosecha,
            cosecha.PesoTotalKg,
            cosecha.PrecioVentaKg,
            cosecha.IngresoTotal));

        return cosecha;
    }

    /// <summary>
    /// Factory method para crear una instancia temporal usada exclusivamente
    /// para invocar <see cref="ValidarBloqueoPorCarencia"/> sin persistir.
    /// </summary>
    public static Cosecha CrearParaValidacion(Guid id, Guid loteId, DateTimeOffset fechaCosecha)
    {
        return new Cosecha
        {
            Id = id,
            LoteId = loteId,
            FechaCosecha = fechaCosecha
        };
    }

    /// <summary>
    /// Valida que ninguna aplicación activa en el lote tenga período de carencia vigente
    /// a la fecha de cosecha. Lanza <see cref="PeriodoCarenciaException"/> si algún insumo
    /// no cumple.
    /// </summary>
    /// <param name="aplicaciones">Aplicaciones del lote con carencia potencialmente activa.</param>
    /// <param name="insumos">Catálogo de insumos para obtener nombre comercial.</param>
    /// <exception cref="PeriodoCarenciaException">Si un insumo tiene período de carencia activo.</exception>
    public void ValidarBloqueoPorCarencia(
        IEnumerable<AplicacionQuimico> aplicaciones,
        IEnumerable<Insumo> insumos)
    {
        var insumosDict = insumos.ToDictionary(i => i.Id);

        foreach (var aplicacion in aplicaciones)
        {
            // Si la fecha fin de carencia de la aplicación es posterior a la fecha de cosecha,
            // el lote está bloqueado.
            if (aplicacion.FechaFinCarencia > FechaCosecha)
            {
                var insumoNombre = insumosDict.TryGetValue(aplicacion.InsumoId, out var insumo)
                    ? insumo.NombreComercial
                    : "Insumo desconocido";

                var diasRestantes = (int)Math.Ceiling((aplicacion.FechaFinCarencia - FechaCosecha).TotalDays);

                BloqueadaPorCarencia = true;

                throw new PeriodoCarenciaException(
                    LoteId,
                    aplicacion.Id,
                    aplicacion.InsumoId,
                    insumoNombre,
                    aplicacion.FechaFinCarencia,
                    diasRestantes);
            }
        }
    }
}
