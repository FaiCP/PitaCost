#nullable enable
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Insumos.Queries.GetInsumos;

/// <summary>
/// Handler para <see cref="GetInsumosQuery"/>.
/// Retorna todos los insumos activos con sus períodos de carencia,
/// aplicando filtros opcionales por tipo de producto y texto de búsqueda.
/// </summary>
public class GetInsumosQueryHandler
    : IRequestHandler<GetInsumosQuery, ApiResponse<IReadOnlyList<InsumoDto>>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<GetInsumosQueryHandler> _logger;

    public GetInsumosQueryHandler(
        IApplicationDbContext dbContext,
        ILogger<GetInsumosQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<IReadOnlyList<InsumoDto>>> Handle(
        GetInsumosQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Insumos
            .Include(i => i.PeriodosCarencia)
            .Where(i => i.Activo)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TipoProducto))
        {
            query = query.Where(i => i.TipoProducto == request.TipoProducto);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(i =>
                i.NombreComercial.ToLower().Contains(search) ||
                i.IngredienteActivo.ToLower().Contains(search));
        }

        var insumos = await query
            .OrderBy(i => i.NombreComercial)
            .Select(i => new InsumoDto
            {
                Id = i.Id,
                NombreComercial = i.NombreComercial,
                IngredienteActivo = i.IngredienteActivo,
                TipoProducto = i.TipoProducto,
                CategoriaToxico = i.CategoriaToxico,
                DosisMinima = i.DosisMinima,
                DosisMaxima = i.DosisMaxima,
                UnidadDosis = i.UnidadDosis,
                PeriodosCarencia = i.PeriodosCarencia.Select(p => new PeriodoCarenciaDto
                {
                    Cultivo = p.Cultivo,
                    DiasCarencia = p.DiasCarencia
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Consulta insumos: {Count} resultados. Filtro: TipoProducto={TipoProducto}, Search={Search}.",
            insumos.Count, request.TipoProducto ?? "N/A", request.Search ?? "N/A");

        return ApiResponse<IReadOnlyList<InsumoDto>>.Ok(insumos);
    }
}
