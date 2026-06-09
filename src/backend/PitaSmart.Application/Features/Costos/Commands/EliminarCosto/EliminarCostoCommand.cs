#nullable enable
using MediatR;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Costos.Commands.EliminarCosto;

/// <summary>
/// Command para eliminar (soft delete) un costo existente.
/// </summary>
public record EliminarCostoCommand : IRequest<ApiResponse<bool>>
{
    /// <summary>ID del costo a eliminar.</summary>
    public Guid Id { get; init; }
}
