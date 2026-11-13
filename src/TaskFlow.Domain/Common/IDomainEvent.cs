namespace TaskFlow.Domain.Common;

/// <summary>
/// A fact that happened inside the domain. Each event has a stable identifier
/// which doubles as the message id on the bus, enabling idempotent consumers.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
