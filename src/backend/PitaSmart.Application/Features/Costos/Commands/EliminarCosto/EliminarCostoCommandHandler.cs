#nullable enable
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Application.Features.Costos.Commands.EliminarCosto;

/// <summary>
/// Handler para <see cref="EliminarCostoCommand"/>.
/// Flujo:
///   1. Obtiene el costo existente.
///   2. Verifica que el lote del costo pertenezca al usuario.
///   3. Ejecuta soft delete.
///   4. Persiste.
/// </summary>
public class EliminarCostoCommandHandler
    : IRequestHandler<EliminarCostoCommand, ApiResponse<bool>>
{
    private readonly ICostoRepository _costoRepository;
    private readonly ILoteRepository _loteRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EliminarCostoCommandHandler> _logger;

    public EliminarCostoCommandHandler(
        ICostoRepository costoRepository,
        ILoteRepository loteRepository,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<EliminarCostoCommandHandler> logger)
    {
        _costoRepository = costoRepository;
        _loteRepository = loteRepository;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<bool>> Handle(
        EliminarCostoCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Obtener el costo existente.
        var costo = await _costoRepository.GetByIdAsync(request.Id, cancellationToken);
        if (costo is null)
        {
            return ApiResponse<bool>.Fail(
                "COSTO_NO_ENCONTRADO",
                $"El costo con ID {request.Id} no existe.");
        }

        // 2. Verificar que el lote pertenezca al usuario.
        var perteneceAlUsuario = await _loteRepository.BelongsToUserAsync(
            costo.LoteId, _currentUserService.UserId, cancellationToken);

        if (!perteneceAlUsuario)
        {
            return ApiResponse<bool>.Fail(
                "ACCESO_DENEGADO",
                "No tiene permisos para eliminar este costo.");
        }

        // 3. Soft delete.
        costo.SoftDelete();

        // 4. Persistir.
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Costo {CostoId} eliminado (soft delete).", costo.Id);

        return ApiResponse<bool>.Ok(true);
    }
}
