#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Aplicaciones.Commands.RegistrarAplicacion;

/// <summary>
/// Comando para registrar una nueva aplicación de insumo agroquímico a un lote.
/// Todas las propiedades corresponden al contrato POST /v1/api/aplicaciones.
/// </summary>
public record RegistrarAplicacionCommand : IRequest<ApiResponse<RegistrarAplicacionResponse>>
{
    /// <summary>UUID generado en cliente (soporte offline, idempotencia).</summary>
    public Guid Id { get; init; }

    /// <summary>ID del lote donde se aplicó el insumo.</summary>
    public Guid LoteId { get; init; }

    /// <summary>ID del insumo agroquímico del catálogo.</summary>
    public Guid InsumoId { get; init; }

    /// <summary>Fecha y hora de aplicación en campo.</summary>
    public DateTimeOffset FechaAplicacion { get; init; }

    /// <summary>Dosis aplicada.</summary>
    public DosisDto Dosis { get; init; } = null!;

    /// <summary>Hectáreas tratadas.</summary>
    public decimal AreaAplicadaHa { get; init; }

    /// <summary>Método: FUMIGACION, DRENCH, INYECCION, GRANULAR, OTRO.</summary>
    public string MetodoAplicacion { get; init; } = string.Empty;

    /// <summary>Nombre del operador.</summary>
    public string OperadorNombre { get; init; } = string.Empty;

    /// <summary>Coordenadas GPS (nullable).</summary>
    public CoordenadasGpsDto? CoordenadasGps { get; init; }

    /// <summary>Observaciones del agricultor.</summary>
    public string? Observaciones { get; init; }

    /// <summary>Costo total en USD.</summary>
    public decimal CostoTotal { get; init; }

    /// <summary>Indica si fue creado sin conexión.</summary>
    public bool CreadoOffline { get; init; }

    /// <summary>Timestamp del cliente al crear el registro.</summary>
    public DateTimeOffset ClientTimestamp { get; init; }
}

/// <summary>DTO para dosis en el request.</summary>
public record DosisDto
{
    public decimal Cantidad { get; init; }
    public string Unidad { get; init; } = string.Empty;
}

/// <summary>DTO para coordenadas GPS en el request.</summary>
public record CoordenadasGpsDto
{
    public double Latitud { get; init; }
    public double Longitud { get; init; }
}

/// <summary>
/// Respuesta exitosa del registro de aplicación.
/// </summary>
public record RegistrarAplicacionResponse
{
    public Guid Id { get; init; }
    public Guid LoteId { get; init; }
    public string LoteNombre { get; init; } = string.Empty;
    public Guid InsumoId { get; init; }
    public string InsumoNombre { get; init; } = string.Empty;
    public DateTimeOffset FechaAplicacion { get; init; }
    public DosisDto Dosis { get; init; } = null!;
    public decimal AreaAplicadaHa { get; init; }
    public string MetodoAplicacion { get; init; } = string.Empty;
    public string OperadorNombre { get; init; } = string.Empty;
    public decimal CostoTotal { get; init; }
    public PeriodoCarenciaResponseDto PeriodoCarencia { get; init; } = null!;
    public string RowVersion { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>DTO del período de carencia en la respuesta.</summary>
public record PeriodoCarenciaResponseDto
{
    public int DiasCarencia { get; init; }
    public DateTimeOffset FechaFinCarencia { get; init; }
    public bool CosechaBloqueada { get; init; }
}
