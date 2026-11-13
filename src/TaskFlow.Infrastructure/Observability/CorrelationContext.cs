namespace TaskFlow.Infrastructure.Observability;

/// <summary>
/// Flows the request correlation id through async calls. The API sets it from the
/// X-Correlation-ID header; it is stored on outbox rows and forwarded as a RabbitMQ
/// header so the worker's logs can be joined with the originating HTTP request.
/// </summary>
public static class CorrelationContext
{
    public const string HeaderName = "X-Correlation-ID";
    public const string LogPropertyName = "CorrelationId";

    private static readonly AsyncLocal<string?> Current = new();

    public static string? CorrelationId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
