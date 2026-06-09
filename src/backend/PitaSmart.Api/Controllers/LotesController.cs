#nullable enable
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitaSmart.Application.Features.Lotes.Commands.RegistrarLote;
using PitaSmart.Application.Features.Lotes.Queries.GetLotesPorUsuario;
using PitaSmart.Application.Features.Lotes.Queries.GetRentabilidad;

namespace PitaSmart.Api.Controllers;

/// <summary>
/// Controlador para gestión de lotes de cultivo.
/// Base path: /v1/api/lotes
/// </summary>
[ApiController]
[Route("v1/api/lotes")]
[Authorize]
[Produces("application/json")]
public class LotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LotesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registra un nuevo lote en una finca del usuario autenticado.
    /// </summary>
    /// <param name="command">Datos del lote a crear.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>201 Created con los datos del lote registrado.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarLoteCommand command,
        CancellationToken cancellationToken)
    {
        var resultado = await _mediator.Send(command, cancellationToken);

        if (!resultado.Success && resultado.Error?.Code == "FINCA_NO_ENCONTRADA")
            return NotFound(resultado);

        return StatusCode(StatusCodes.Status201Created, resultado);
    }

    /// <summary>
    /// Obtiene todos los lotes activos del usuario autenticado.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>200 OK con la lista de lotes.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetLotes(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLotesPorUsuarioQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene el cálculo de rentabilidad completo para un lote en un período.
    /// Incluye ingresos, costos con desglose por categoría, y alertas de período de carencia.
    /// </summary>
    /// <param name="id">ID del lote.</param>
    /// <param name="desde">Fecha inicio del período (YYYY-MM-DD). Default: inicio del ciclo de cultivo.</param>
    /// <param name="hasta">Fecha fin del período (YYYY-MM-DD). Default: hoy.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>200 OK con datos de rentabilidad.</returns>
    /// <response code="200">Cálculo de rentabilidad exitoso.</response>
    /// <response code="404">El lote no existe.</response>
    [HttpGet("{id:guid}/rentabilidad")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRentabilidad(
        Guid id,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken cancellationToken)
    {
        var query = new GetRentabilidadQuery
        {
            LoteId = id,
            Desde = desde,
            Hasta = hasta
        };

        var result = await _mediator.Send(query, cancellationToken);

        if (!result.Success && result.Error?.Code == "LOTE_NO_ENCONTRADO")
            return NotFound(result);

        return Ok(result);
    }
}
