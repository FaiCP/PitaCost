#nullable enable
namespace PitaSmart.Application.Common.Models;

/// <summary>
/// Respuesta estándar de la API según el contrato definido.
/// </summary>
public record ApiResponse<T>
{
    /// <summary>Indica si la operación fue exitosa.</summary>
    public bool Success { get; init; }

    /// <summary>Datos de la respuesta (null en caso de error).</summary>
    public T? Data { get; init; }

    /// <summary>Información del error (null en caso de éxito).</summary>
    public ApiError? Error { get; init; }

    /// <summary>Timestamp del servidor.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public static ApiResponse<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    public static ApiResponse<T> Fail(string code, string message, List<ApiErrorDetail>? details = null) => new()
    {
        Success = false,
        Error = new ApiError(code, message, details)
    };
}

/// <summary>Detalle de error en la respuesta API.</summary>
public record ApiError(string Code, string Message, List<ApiErrorDetail>? Details = null);

/// <summary>Error específico de un campo.</summary>
public record ApiErrorDetail(string Field, string Message);
