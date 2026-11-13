using Serilog.Context;
using TaskFlow.Infrastructure.Observability;

namespace TaskFlow.Api.Middleware;

/// <summary>
/// Accepts an incoming X-Correlation-ID (or creates one), echoes it on the response,
/// pushes it into every log event, and stores it for the outbox so the background
/// worker logs the same id when it processes the resulting event.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationContext.HeaderName].FirstOrDefault();
        var correlationId = IsValid(incoming) ? incoming! : Guid.NewGuid().ToString("N");

        CorrelationContext.CorrelationId = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationContext.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelationContext.LogPropertyName, correlationId))
        {
            await next(context);
        }
    }

    // Reject oversized or unusual values to prevent log injection.
    private static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
}

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        return next(context);
    }
}
