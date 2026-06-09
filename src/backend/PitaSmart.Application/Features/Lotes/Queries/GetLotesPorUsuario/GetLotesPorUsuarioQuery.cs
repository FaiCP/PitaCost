#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Lotes.Queries.GetLotesPorUsuario;

/// <summary>
/// Query para obtener todos los lotes activos del usuario autenticado.
/// Endpoint: GET /v1/api/lotes
/// </summary>
public record GetLotesPorUsuarioQuery : IRequest<ApiResponse<IReadOnlyList<LoteResumenDto>>>
{
}

/// <summary>
/// DTO resumido de lote para listado.
/// </summary>
public record LoteResumenDto
{
    public Guid Id { get; init; }
    public Guid FincaId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Cultivo { get; init; } = string.Empty;
    public decimal AreaHa { get; init; }
    public bool Activo { get; init; }
}
