#nullable enable
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitaSmart.Application.Features.Fincas.Commands.RegistrarFinca;

namespace PitaSmart.Api.Controllers;

/// <summary>
/// Controlador de gestión de fincas.
/// Base path: /v1/api/fincas
/// </summary>
[ApiController]
[Route("v1/api/fincas")]
[Authorize]
[Produces("application/json")]
public class FincasController : ControllerBase
{
    private readonly ISender _mediator;

    public FincasController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra una nueva finca asociada al usuario autenticado.
    /// </summary>
    /// <param name="command">Datos de la finca a crear.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>201 Created con los datos de la finca registrada.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarFincaCommand command,
        CancellationToken cancellationToken)
    {
        var resultado = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, resultado);
    }
}
