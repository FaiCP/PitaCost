#nullable enable
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using PitaSmart.Application.Common.Models;
using PitaSmart.Domain.Exceptions;

namespace PitaSmart.Api.Middleware;

/// <summary>
/// Middleware global de manejo de excepciones. Convierte excepciones del dominio y de validación
/// en respuestas JSON estandarizadas según el contrato de la API.
/// Orden de prioridad: PeriodoCarenciaException > DomainException > ValidationException > Exception.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Invoca el pipeline y captura excepciones no controladas.</summary>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (PeriodoCarenciaException ex)
        {
            _logger.LogWarning(ex,
                "Bloqueo por periodo de carencia en lote {LoteId}, insumo {InsumoNombre}",
                ex.LoteId, ex.InsumoNombre);

            await WriteErrorResponseAsync(httpContext, HttpStatusCode.UnprocessableEntity, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError(ex.Code, ex.Message, new List<ApiErrorDetail>
                {
                    new("loteId", $"El lote tiene una aplicacion con periodo de carencia vigente hasta {ex.FechaFinCarencia:yyyy-MM-dd}."),
                    new("insumoId", $"Insumo infractor: {ex.InsumoNombre}. Dias restantes: {ex.DiasRestantes}.")
                })
            });
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Excepcion de dominio: {Code} - {Message}", ex.Code, ex.Message);

            await WriteErrorResponseAsync(httpContext, HttpStatusCode.UnprocessableEntity, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError(ex.Code, ex.Message)
            });
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Error de validacion: {Errors}",
                string.Join("; ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

            var details = ex.Errors
                .Select(e => new ApiErrorDetail(e.PropertyName, e.ErrorMessage))
                .ToList();

            await WriteErrorResponseAsync(httpContext, HttpStatusCode.BadRequest, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError("VALIDATION_ERROR", "Uno o mas campos tienen errores de validacion.", details)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interno no controlado.");

            await WriteErrorResponseAsync(httpContext, HttpStatusCode.InternalServerError, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError("INTERNAL_ERROR", "Ha ocurrido un error interno. Intente nuevamente.")
            });
        }
    }

    private static async Task WriteErrorResponseAsync<T>(
        HttpContext httpContext, HttpStatusCode statusCode, ApiResponse<T> response)
    {
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = (int)statusCode;
        var json = JsonSerializer.Serialize(response, JsonOptions);
        await httpContext.Response.WriteAsync(json);
    }
}
