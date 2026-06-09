#nullable enable
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PitaSmart.Application.Features.Sync.Commands.ProcessSyncBatch;
using PitaSmart.Application.Features.Sync.Queries.GetSyncPull;

namespace PitaSmart.Api.Controllers;

/// <summary>
/// Controlador de sincronización offline-first.
/// Base path: /v1/api/sync
/// </summary>
[ApiController]
[Route("v1/api/sync")]
[Authorize]
[Produces("application/json")]
public class SyncController : ControllerBase
{
    private readonly IMediator _mediator;

    public SyncController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Recibe un batch de operaciones pendientes del dispositivo offline y las procesa en orden.
    /// Cada operación tiene su resultado individual (APLICADA, DUPLICADA, CONFLICTO, RECHAZADA, ERROR).
    /// Máximo 100 operaciones por request.
    /// </summary>
    /// <param name="command">Batch de operaciones offline.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>200 OK con resultado individual por operación.</returns>
    /// <response code="200">Batch procesado (ver estado de cada operación).</response>
    /// <response code="400">Error de validación en la estructura del batch.</response>
    /// <response code="401">Usuario no autenticado.</response>
    /// <response code="429">Rate limit excedido (máx 10 sync push/min por dispositivo).</response>
    [HttpPost("push")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Push(
        [FromBody] ProcessSyncBatchCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene todos los datos del usuario para popular RxDB en el cliente.
    /// Incluye lotes, insumos, aplicaciones, cosechas y costos.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>200 OK con todos los datos de sincronización.</returns>
    /// <response code="200">Datos de sincronización completos.</response>
    /// <response code="401">Usuario no autenticado.</response>
    [HttpGet("pull")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Pull(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSyncPullQuery(), cancellationToken);
        return Ok(result);
    }
}
