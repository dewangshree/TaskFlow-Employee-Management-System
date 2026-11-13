namespace TaskFlow.Domain.Entities;

/// <summary>
/// Transactional outbox row. Written in the same transaction as the business
/// change; a background dispatcher publishes it to RabbitMQ afterwards, which
/// guarantees at-least-once delivery without distributed transactions.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Inbox/deduplication record for idempotent message consumers.</summary>
public class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
