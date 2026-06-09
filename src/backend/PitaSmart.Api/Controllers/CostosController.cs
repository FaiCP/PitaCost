#nullable enable
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitaSmart.Application.Features.Costos.Commands.ActualizarCosto;
using PitaSmart.Application.Features.Costos.Commands.EliminarCosto;
using PitaSmart.Application.Features.Costos.Commands.RegistrarCosto;

namespace PitaSmart.Api.Controllers;

/// <summary>
/// Controlador de costos de lotes.
/// Base path: /v1/api/costos
/// </summary>
[ApiController]
[Route("v1/api/costos")]
[Authorize]
[Produces("application/json")]
public class CostosController : ControllerBase
{
    private readonly ISender _mediator;

    public CostosController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra un nuevo costo en un lote.
    /// Soporta idempotencia: si el ID ya existe, retorna el costo existente.
    /// </summary>
    /// <param name="command">Datos del costo a registrar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>201 Created con los datos del costo registrado.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarCostoCommand command,
        CancellationToken cancellationToken)
    {
        var resultado = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, resultado);
    }

    /// <summary>
    /// Actualiza un costo existente.
    /// Solo se pueden actualizar costos de lotes que pertenezcan al usuario autenticado.
    /// </summary>
    /// <param name="id">ID del costo a actualizar.</param>
    /// <param name="command">Datos actualizados del costo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>200 OK con los datos del costo actualizado.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] ActualizarCostoCommand command,
        CancellationToken cancellationToken)
    {
        // Asegurar que el ID de la ruta coincida con el del body.
        var commandConId = command with { Id = id };
        var resultado = await _mediator.Send(commandConId, cancellationToken);

        if (!resultado.Success)
        {
            return resultado.Error?.Code switch
            {
                "COSTO_NO_ENCONTRADO" => NotFound(resultado),
                "ACCESO_DENEGADO" => StatusCode(StatusCodes.Status403Forbidden, resultado),
                _ => BadRequest(resultado)
            };
        }

        return Ok(resultado);
    }

    /// <summary>
    /// Elimina un costo (soft delete).
    /// Solo se pueden eliminar costos de lotes que pertenezcan al usuario autenticado.
    /// </summary>
    /// <param name="id">ID del costo a eliminar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new EliminarCostoCommand { Id = id };
        var resultado = await _mediator.Send(command, cancellationToken);

        if (!resultado.Success)
        {
            return resultado.Error?.Code switch
            {
                "COSTO_NO_ENCONTRADO" => NotFound(resultado),
                "ACCESO_DENEGADO" => StatusCode(StatusCodes.Status403Forbidden, resultado),
                _ => BadRequest(resultado)
            };
        }

        return NoContent();
    }
}
