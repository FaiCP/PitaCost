#nullable enable
namespace PitaSmart.Api.Middleware;

/// <summary>
/// Middleware que propaga el header X-Correlation-Id del request al response.
/// Si el cliente no envía uno, el servidor genera uno automáticamente.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId)
            || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId.ToString();
            return Task.CompletedTask;
        });

        // Hacer disponible en el scope de logging.
        using (context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("CorrelationId").BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId.ToString()!
            }))
        {
            await _next(context);
        }
    }
}
