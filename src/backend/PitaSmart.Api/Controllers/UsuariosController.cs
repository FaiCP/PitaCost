#nullable enable
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PitaSmart.Application.Features.Usuarios.Commands.RegistrarUsuario;

namespace PitaSmart.Api.Controllers;

/// <summary>
/// Controlador de gestión de usuarios.
/// Base path: /v1/api/usuarios
/// </summary>
[ApiController]
[Route("v1/api/usuarios")]
[Produces("application/json")]
public class UsuariosController : ControllerBase
{
    private readonly ISender _mediator;

    public UsuariosController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra un nuevo usuario en el sistema.
    /// Endpoint público (no requiere autenticación).
    /// </summary>
    /// <param name="command">Datos del usuario a registrar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>201 Created con los datos del usuario registrado, o 409 Conflict si el email ya existe.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarUsuarioCommand command,
        CancellationToken cancellationToken)
    {
        var resultado = await _mediator.Send(command, cancellationToken);

        if (!resultado.Success && resultado.Error?.Code == "EMAIL_DUPLICADO")
            return Conflict(resultado);

        return StatusCode(StatusCodes.Status201Created, resultado);
    }
}
