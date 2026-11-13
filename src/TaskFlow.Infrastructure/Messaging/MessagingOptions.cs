namespace TaskFlow.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Exchange { get; set; } = "taskflow.events";
    public string NotificationsQueue { get; set; } = "taskflow.notifications";
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>Delay before each retry attempt; its length is the max retry count.</summary>
    public int[] RetryDelaysSeconds { get; set; } = [5, 30, 120];

    public string DeadLetterQueue => $"{NotificationsQueue}.dlq";
    public string RetryQueue(int attempt) => $"{NotificationsQueue}.retry.{attempt}";
}

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollingIntervalMilliseconds { get; set; } = 2000;
    public int BatchSize { get; set; } = 50;
    public int MaxAttempts { get; set; } = 20;
    public int BacklogWarningSeconds { get; set; } = 120;
    public int RetentionDays { get; set; } = 7;
}

public static class MessageHeaders
{
    public const string RetryCount = "x-retry-count";
    public const string LastError = "x-last-error";
    public const string CorrelationId = "x-correlation-id";
}
