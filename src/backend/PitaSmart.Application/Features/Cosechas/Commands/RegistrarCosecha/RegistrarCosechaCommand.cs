#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Cosechas.Commands.RegistrarCosecha;

/// <summary>
/// Comando para registrar una nueva cosecha en un lote.
/// CRITICO: El handler valida el Periodo de Carencia antes de persistir.
/// Todas las propiedades corresponden al contrato POST /v1/api/cosechas.
/// </summary>
public record RegistrarCosechaCommand : IRequest<ApiResponse<RegistrarCosechaResponse>>
{
    /// <summary>UUID generado en cliente (soporte offline, idempotencia).</summary>
    public Guid Id { get; init; }

    /// <summary>ID del lote a cosechar.</summary>
    public Guid LoteId { get; init; }

    /// <summary>Fecha de la cosecha.</summary>
    public DateTimeOffset FechaCosecha { get; init; }

    /// <summary>Peso total cosechado en kilogramos.</summary>
    public decimal PesoTotalKg { get; init; }

    /// <summary>Grado de calidad: PREMIUM, PRIMERA, SEGUNDA, RECHAZO.</summary>
    public string CalidadGrado { get; init; } = string.Empty;

    /// <summary>Nombre del comprador (opcional).</summary>
    public string? Comprador { get; init; }

    /// <summary>Precio de venta por kilogramo en USD (opcional).</summary>
    public decimal? PrecioVentaKg { get; init; }

    /// <summary>Observaciones del agricultor.</summary>
    public string? Observaciones { get; init; }

    /// <summary>Indica si fue creado sin conexion.</summary>
    public bool CreadoOffline { get; init; }

    /// <summary>Timestamp del cliente al crear el registro.</summary>
    public DateTimeOffset ClientTimestamp { get; init; }
}

/// <summary>
/// Respuesta exitosa del registro de cosecha.
/// </summary>
public record RegistrarCosechaResponse
{
    /// <summary>ID de la cosecha registrada.</summary>
    public Guid Id { get; init; }

    /// <summary>ID del lote cosechado.</summary>
    public Guid LoteId { get; init; }

    /// <summary>Nombre del lote.</summary>
    public string LoteNombre { get; init; } = string.Empty;

    /// <summary>Fecha de la cosecha.</summary>
    public DateTimeOffset FechaCosecha { get; init; }

    /// <summary>Peso total en kg.</summary>
    public decimal PesoTotalKg { get; init; }

    /// <summary>Grado de calidad.</summary>
    public string CalidadGrado { get; init; } = string.Empty;

    /// <summary>Nombre del comprador.</summary>
    public string? Comprador { get; init; }

    /// <summary>Precio de venta por kg.</summary>
    public decimal? PrecioVentaKg { get; init; }

    /// <summary>Ingreso total calculado (PesoTotalKg * PrecioVentaKg).</summary>
    public decimal? IngresoTotal { get; init; }

    /// <summary>Indica si la cosecha fue bloqueada por carencia (siempre false si se registro exitosamente).</summary>
    public bool BloqueadaPorCarencia { get; init; }

    /// <summary>Token de concurrencia optimista (Base64).</summary>
    public string RowVersion { get; init; } = string.Empty;

    /// <summary>Fecha de creacion en el servidor.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
