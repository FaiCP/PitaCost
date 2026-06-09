#nullable enable
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Costos.Entities;

namespace PitaSmart.Application.Features.Lotes.Commands.RegistrarLote;

/// <summary>
/// Handler para <see cref="RegistrarLoteCommand"/>.
/// Flujo:
///   1. Verifica que la finca exista y pertenezca al usuario autenticado.
///   2. Crea la entidad Lote.
///   3. Persiste y retorna respuesta.
/// </summary>
public class RegistrarLoteCommandHandler
    : IRequestHandler<RegistrarLoteCommand, ApiResponse<RegistrarLoteResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RegistrarLoteCommandHandler> _logger;

    public RegistrarLoteCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<RegistrarLoteCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<RegistrarLoteResponse>> Handle(
        RegistrarLoteCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verificar que la finca exista y pertenezca al usuario.
        var fincaExiste = await _dbContext.Fincas
            .AnyAsync(f => f.Id == request.FincaId && f.UsuarioId == _currentUserService.UserId, cancellationToken);

        if (!fincaExiste)
        {
            return ApiResponse<RegistrarLoteResponse>.Fail(
                "FINCA_NO_ENCONTRADA",
                $"La finca con ID {request.FincaId} no existe o no pertenece al usuario.");
        }

        // 2. Crear entidad de dominio.
        var lote = Lote.Crear(
            id: Guid.NewGuid(),
            fincaId: request.FincaId,
            nombre: request.Nombre,
            cultivo: request.Cultivo,
            areaHa: request.AreaHa,
            latitud: request.Latitud,
            longitud: request.Longitud,
            fechaInicioSiembra: request.FechaInicioSiembra);

        // 3. Persistir.
        await _dbContext.Lotes.AddAsync(lote, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Lote {LoteId} creado en finca {FincaId}. Cultivo: {Cultivo}, Area: {AreaHa} ha.",
            lote.Id, lote.FincaId, lote.Cultivo, lote.AreaHa);

        return ApiResponse<RegistrarLoteResponse>.Ok(new RegistrarLoteResponse
        {
            Id = lote.Id,
            FincaId = lote.FincaId,
            Nombre = lote.Nombre,
            Cultivo = lote.Cultivo,
            AreaHa = lote.AreaHa,
            Latitud = request.Latitud,
            Longitud = request.Longitud,
            FechaInicioSiembra = lote.FechaInicioSiembra,
            Activo = true
        });
    }
}
