#nullable enable
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Identidad.Entities;
using PitaSmart.Domain.Identidad.Interfaces;

namespace PitaSmart.Application.Features.Usuarios.Commands.RegistrarUsuario;

/// <summary>
/// Handler para <see cref="RegistrarUsuarioCommand"/>.
/// Flujo:
///   1. Verifica que el email no esté registrado.
///   2. Crea la entidad Usuario.
///   3. Persiste y retorna respuesta.
/// </summary>
public class RegistrarUsuarioCommandHandler
    : IRequestHandler<RegistrarUsuarioCommand, ApiResponse<RegistrarUsuarioResponse>>
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<RegistrarUsuarioCommandHandler> _logger;

    public RegistrarUsuarioCommandHandler(
        IUsuarioRepository usuarioRepository,
        IApplicationDbContext dbContext,
        ILogger<RegistrarUsuarioCommandHandler> logger)
    {
        _usuarioRepository = usuarioRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<RegistrarUsuarioResponse>> Handle(
        RegistrarUsuarioCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verificar unicidad del email.
        var existente = await _usuarioRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existente is not null)
        {
            return ApiResponse<RegistrarUsuarioResponse>.Fail(
                "EMAIL_DUPLICADO",
                $"Ya existe un usuario registrado con el email {request.Email}.");
        }

        // 2. Crear entidad de dominio.
        var usuario = Usuario.Crear(
            id: Guid.NewGuid(),
            email: request.Email,
            nombreCompleto: request.NombreCompleto,
            cedula: request.Cedula,
            telefono: request.Telefono);

        // 3. Persistir.
        await _usuarioRepository.AddUsuarioAsync(usuario, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Usuario {UsuarioId} registrado con email {Email}.", usuario.Id, usuario.Email);

        return ApiResponse<RegistrarUsuarioResponse>.Ok(new RegistrarUsuarioResponse
        {
            Id = usuario.Id,
            Email = usuario.Email,
            NombreCompleto = usuario.NombreCompleto,
            Rol = usuario.Rol
        });
    }
}
