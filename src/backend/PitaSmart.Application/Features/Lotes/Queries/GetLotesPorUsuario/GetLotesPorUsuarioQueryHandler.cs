#nullable enable
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;

namespace PitaSmart.Application.Features.Lotes.Queries.GetLotesPorUsuario;

/// <summary>
/// Handler para <see cref="GetLotesPorUsuarioQuery"/>.
/// Retorna todos los lotes activos de las fincas del usuario autenticado.
/// </summary>
public class GetLotesPorUsuarioQueryHandler
    : IRequestHandler<GetLotesPorUsuarioQuery, ApiResponse<IReadOnlyList<LoteResumenDto>>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetLotesPorUsuarioQueryHandler> _logger;

    public GetLotesPorUsuarioQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<GetLotesPorUsuarioQueryHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<IReadOnlyList<LoteResumenDto>>> Handle(
        GetLotesPorUsuarioQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;

        var lotes = await _dbContext.Lotes
            .Include(l => l.Finca)
            .Where(l => l.Finca.UsuarioId == userId && l.Activo)
            .OrderBy(l => l.Nombre)
            .Select(l => new LoteResumenDto
            {
                Id = l.Id,
                FincaId = l.FincaId,
                Nombre = l.Nombre,
                Cultivo = l.Cultivo,
                AreaHa = l.AreaHa,
                Activo = l.Activo
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Consulta lotes del usuario {UserId}: {Count} resultados.", userId, lotes.Count);

        return ApiResponse<IReadOnlyList<LoteResumenDto>>.Ok(lotes);
    }
}
