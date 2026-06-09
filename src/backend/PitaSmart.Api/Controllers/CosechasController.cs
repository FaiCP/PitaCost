#nullable enable
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitaSmart.Application.Features.Cosechas.Commands.RegistrarCosecha;
using PitaSmart.Application.Features.Cosechas.Queries.GetCosechasPorLote;

namespace PitaSmart.Api.Controllers;

/// <summary>
/// Controlador para gestion de cosechas.
/// Base path: /v1/api/cosechas
/// CRITICO: El registro de cosecha valida el Periodo de Carencia de Agrocalidad.
/// </summary>
[ApiController]
[Route("v1/api/cosechas")]
[Authorize]
[Produces("application/json")]
public class CosechasController : ControllerBase
{
    private readonly IMediator _mediator;

    public CosechasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra una nueva cosecha. Valida periodo de carencia antes de persistir.
    /// Si alguna aplicacion en el lote tiene carencia activa, retorna 422.
    /// </summary>
    /// <param name="command">Datos de la cosecha segun contrato API.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <returns>201 Created con datos de la cosecha registrada.</returns>
    /// <response code="201">Cosecha registrada exitosamente.</response>
    /// <response code="400">Error de validacion en los datos de entrada.</response>
    /// <response code="401">Usuario no autenticado.</response>
    /// <response code="422">Periodo de carencia activo impide la cosecha.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarCosechaCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
            return UnprocessableEntity(result);

        return CreatedAtAction(nameof(GetPorLote), new { loteId = result.Data!.LoteId }, result);
    }

    /// <summary>
    /// Obtiene las cosechas de un lote especifico, ordenadas por fecha descendente.
    /// </summary>
    /// <param name="loteId">ID del lote a consultar.</param>
    /// <param name="cancellationToken">Token de cancelacion.</param>
    /// <returns>200 OK con lista de cosechas del lote.</returns>
    /// <response code="200">Lista de cosechas obtenida exitosamente.</response>
    /// <response code="401">Usuario no autenticado.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPorLote(
        [FromQuery] Guid loteId,
        CancellationToken cancellationToken)
    {
        if (loteId == Guid.Empty)
            return BadRequest(Application.Common.Models.ApiResponse<object>.Fail(
                "VALIDATION_ERROR", "El parametro loteId es requerido."));

        var query = new GetCosechasPorLoteQuery { LoteId = loteId };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
