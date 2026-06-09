#nullable enable
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitaSmart.Application.Features.Insumos.Queries.GetInsumos;

namespace PitaSmart.Api.Controllers;

/// <summary>
/// Controlador del catálogo de insumos agroquímicos.
/// Base path: /v1/api/insumos
/// </summary>
[ApiController]
[Route("v1/api/insumos")]
[Authorize]
[Produces("application/json")]
public class InsumosController : ControllerBase
{
    private readonly ISender _mediator;

    public InsumosController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Obtiene el catálogo de insumos activos con filtros opcionales.
    /// </summary>
    /// <param name="tipoProducto">Filtro opcional por tipo de producto (FUNGICIDA, HERBICIDA, etc.).</param>
    /// <param name="search">Búsqueda opcional por nombre comercial o ingrediente activo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>200 OK con la lista de insumos.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetInsumos(
        [FromQuery] string? tipoProducto,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = new GetInsumosQuery
        {
            TipoProducto = tipoProducto,
            Search = search
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
