#nullable enable
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Application.Features.Costos.Commands.ActualizarCosto;

/// <summary>
/// Handler para <see cref="ActualizarCostoCommand"/>.
/// Flujo:
///   1. Obtiene el costo existente.
///   2. Verifica que el lote del costo pertenezca al usuario.
///   3. Actualiza los campos editables.
///   4. Persiste y retorna respuesta.
/// </summary>
public class ActualizarCostoCommandHandler
    : IRequestHandler<ActualizarCostoCommand, ApiResponse<ActualizarCostoResponse>>
{
    private readonly ICostoRepository _costoRepository;
    private readonly ILoteRepository _loteRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ActualizarCostoCommandHandler> _logger;

    public ActualizarCostoCommandHandler(
        ICostoRepository costoRepository,
        ILoteRepository loteRepository,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<ActualizarCostoCommandHandler> logger)
    {
        _costoRepository = costoRepository;
        _loteRepository = loteRepository;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<ActualizarCostoResponse>> Handle(
        ActualizarCostoCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Obtener el costo existente.
        var costo = await _costoRepository.GetByIdAsync(request.Id, cancellationToken);
        if (costo is null)
        {
            return ApiResponse<ActualizarCostoResponse>.Fail(
                "COSTO_NO_ENCONTRADO",
                $"El costo con ID {request.Id} no existe.");
        }

        // 2. Verificar que el lote pertenezca al usuario.
        var perteneceAlUsuario = await _loteRepository.BelongsToUserAsync(
            costo.LoteId, _currentUserService.UserId, cancellationToken);

        if (!perteneceAlUsuario)
        {
            return ApiResponse<ActualizarCostoResponse>.Fail(
                "ACCESO_DENEGADO",
                "No tiene permisos para modificar este costo.");
        }

        // 3. Actualizar campos editables.
        costo.Actualizar(request.Descripcion, request.Monto, request.Categoria, request.Fecha);

        // 4. Persistir.
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Costo {CostoId} actualizado. Categoria: {Categoria}, Monto: {Monto} USD.",
            costo.Id, costo.Categoria, costo.Monto);

        return ApiResponse<ActualizarCostoResponse>.Ok(new ActualizarCostoResponse
        {
            Id = costo.Id,
            LoteId = costo.LoteId,
            Fecha = costo.Fecha,
            Categoria = costo.Categoria,
            Descripcion = costo.Descripcion,
            Monto = costo.Monto,
            RowVersion = Convert.ToBase64String(costo.RowVersion)
        });
    }
}
