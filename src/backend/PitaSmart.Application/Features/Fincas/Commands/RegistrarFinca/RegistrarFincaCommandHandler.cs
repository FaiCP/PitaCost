#nullable enable
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Costos.Entities;

namespace PitaSmart.Application.Features.Fincas.Commands.RegistrarFinca;

/// <summary>
/// Handler para <see cref="RegistrarFincaCommand"/>.
/// Crea una finca asociada al usuario autenticado actual.
/// </summary>
public class RegistrarFincaCommandHandler
    : IRequestHandler<RegistrarFincaCommand, ApiResponse<RegistrarFincaResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RegistrarFincaCommandHandler> _logger;

    public RegistrarFincaCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<RegistrarFincaCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<RegistrarFincaResponse>> Handle(
        RegistrarFincaCommand request,
        CancellationToken cancellationToken)
    {
        var finca = Finca.Crear(
            id: Guid.NewGuid(),
            usuarioId: _currentUserService.UserId,
            nombre: request.Nombre,
            provincia: request.Provincia,
            canton: request.Canton,
            parroquia: request.Parroquia,
            areaTotalHa: request.AreaTotalHa);

        await _dbContext.Fincas.AddAsync(finca, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Finca {FincaId} creada para usuario {UsuarioId}. Nombre: {Nombre}.",
            finca.Id, _currentUserService.UserId, finca.Nombre);

        return ApiResponse<RegistrarFincaResponse>.Ok(new RegistrarFincaResponse
        {
            Id = finca.Id,
            Nombre = finca.Nombre,
            Provincia = finca.Provincia,
            Canton = finca.Canton,
            Parroquia = finca.Parroquia,
            AreaTotalHa = finca.AreaTotalHa
        });
    }
}
