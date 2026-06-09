#nullable enable
using MediatR;
using Microsoft.Extensions.Logging;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Costos.Entities;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Application.Features.Costos.Commands.RegistrarCosto;

/// <summary>
/// Handler para <see cref="RegistrarCostoCommand"/>.
/// Flujo:
///   1. Idempotencia: si ya existe el costo con este ID, retorna el existente.
///   2. Verifica que el lote exista y pertenezca al usuario.
///   3. Crea la entidad CostoLote.
///   4. Persiste y retorna respuesta.
/// </summary>
public class RegistrarCostoCommandHandler
    : IRequestHandler<RegistrarCostoCommand, ApiResponse<RegistrarCostoResponse>>
{
    private readonly ICostoRepository _costoRepository;
    private readonly ILoteRepository _loteRepository;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<RegistrarCostoCommandHandler> _logger;

    public RegistrarCostoCommandHandler(
        ICostoRepository costoRepository,
        ILoteRepository loteRepository,
        IApplicationDbContext dbContext,
        ILogger<RegistrarCostoCommandHandler> logger)
    {
        _costoRepository = costoRepository;
        _loteRepository = loteRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<RegistrarCostoResponse>> Handle(
        RegistrarCostoCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Idempotencia: si ya existe el costo con este ID, retornar sin reprocesar.
        if (await _costoRepository.ExistsByIdAsync(request.Id, cancellationToken))
        {
            _logger.LogInformation(
                "Costo {CostoId} ya existe. Retornando como idempotente.", request.Id);

            var existing = await _costoRepository.GetByIdAsync(request.Id, cancellationToken);
            var existingLote = await _loteRepository.GetByIdAsync(existing!.LoteId, cancellationToken);

            return ApiResponse<RegistrarCostoResponse>.Ok(BuildResponse(existing, existingLote?.Nombre ?? "N/A"));
        }

        // 2. Verificar que el lote exista.
        var lote = await _loteRepository.GetByIdAsync(request.LoteId, cancellationToken)
            ?? throw new InvalidOperationException($"Lote {request.LoteId} no encontrado tras validacion.");

        // 3. Crear la entidad de dominio.
        var costo = CostoLote.Crear(
            id: request.Id,
            loteId: request.LoteId,
            fecha: request.Fecha,
            categoria: request.Categoria,
            descripcion: request.Descripcion,
            monto: request.Monto,
            aplicacionId: request.AplicacionId,
            creadoOffline: request.CreadoOffline,
            clientTimestamp: request.ClientTimestamp);

        // 4. Persistir.
        await _costoRepository.AddCostoAsync(costo, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Costo {CostoId} registrado en lote {LoteId}. Categoria: {Categoria}, Monto: {Monto} USD.",
            costo.Id, costo.LoteId, costo.Categoria, costo.Monto);

        return ApiResponse<RegistrarCostoResponse>.Ok(BuildResponse(costo, lote.Nombre));
    }

    private static RegistrarCostoResponse BuildResponse(CostoLote costo, string loteNombre)
    {
        return new RegistrarCostoResponse
        {
            Id = costo.Id,
            LoteId = costo.LoteId,
            LoteNombre = loteNombre,
            Fecha = costo.Fecha,
            Categoria = costo.Categoria,
            Descripcion = costo.Descripcion,
            Monto = costo.Monto,
            RowVersion = Convert.ToBase64String(costo.RowVersion),
            CreatedAt = costo.CreatedAt
        };
    }
}
