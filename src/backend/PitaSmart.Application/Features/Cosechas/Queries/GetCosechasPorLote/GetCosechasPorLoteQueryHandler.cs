#nullable enable
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Cosecha.Interfaces;

namespace PitaSmart.Application.Features.Cosechas.Queries.GetCosechasPorLote;

/// <summary>
/// Handler para <see cref="GetCosechasPorLoteQuery"/>.
/// Retorna todas las cosechas de un lote ordenadas por fecha descendente.
/// </summary>
public class GetCosechasPorLoteQueryHandler
    : IRequestHandler<GetCosechasPorLoteQuery, ApiResponse<IReadOnlyList<CosechaDto>>>
{
    private readonly ICosechaRepository _cosechaRepository;
    private readonly ILogger<GetCosechasPorLoteQueryHandler> _logger;

    public GetCosechasPorLoteQueryHandler(
        ICosechaRepository cosechaRepository,
        ILogger<GetCosechasPorLoteQueryHandler> logger)
    {
        _cosechaRepository = cosechaRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<IReadOnlyList<CosechaDto>>> Handle(
        GetCosechasPorLoteQuery request,
        CancellationToken cancellationToken)
    {
        var cosechas = await _cosechaRepository.GetByLoteIdAsync(request.LoteId, cancellationToken);

        var dtos = cosechas
            .Select(c => new CosechaDto
            {
                Id = c.Id,
                LoteId = c.LoteId,
                FechaCosecha = c.FechaCosecha,
                PesoTotalKg = c.PesoTotalKg,
                CalidadGrado = c.CalidadGrado,
                Comprador = c.Comprador,
                PrecioVentaKg = c.PrecioVentaKg,
                IngresoTotal = c.IngresoTotal,
                Observaciones = c.Observaciones,
                BloqueadaPorCarencia = c.BloqueadaPorCarencia,
                RowVersion = Convert.ToBase64String(c.RowVersion),
                CreatedAt = c.CreatedAt
            })
            .ToList();

        _logger.LogInformation(
            "Consulta cosechas del lote {LoteId}: {Count} resultados.", request.LoteId, dtos.Count);

        return ApiResponse<IReadOnlyList<CosechaDto>>.Ok(dtos);
    }
}
