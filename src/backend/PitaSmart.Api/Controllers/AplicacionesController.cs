#nullable enable
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitaSmart.Application.Features.Aplicaciones.Commands.RegistrarAplicacion;

namespace PitaSmart.Api.Controllers;

/// <summary>
/// Controlador para gestión de aplicaciones de insumos agroquímicos.
/// Base path: /v1/api/aplicaciones
/// </summary>
[ApiController]
[Route("v1/api/aplicaciones")]
[Authorize]
[Produces("application/json")]
public class AplicacionesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AplicacionesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra una nueva aplicación de insumo agroquímico a un lote.
    /// Soporta idempotencia: si el ID ya existe con payload idéntico, retorna 201 sin reprocesar.
    /// </summary>
    /// <param name="command">Datos de la aplicación según contrato API.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>201 Created con datos de la aplicación registrada y período de carencia calculado.</returns>
    /// <response code="201">Aplicación registrada exitosamente.</response>
    /// <response code="400">Error de validación en los datos de entrada.</response>
    /// <response code="401">Usuario no autenticado.</response>
    /// <response code="422">Regla de negocio violada (ej: dosis excedida).</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarAplicacionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
            return UnprocessableEntity(result);

        return CreatedAtAction(nameof(Registrar), new { id = result.Data!.Id }, result);
    }
}
