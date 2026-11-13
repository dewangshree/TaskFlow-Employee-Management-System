using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Messaging;

/// <summary>Destination for outbox messages: RabbitMQ in the full setup, or in-process when no broker is configured.</summary>
public interface IOutboxPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken ct);
}

/// <summary>
/// Broker-less fallback for small/free deployments: hands the event straight to the same
/// idempotent notification processor the RabbitMQ consumer uses. The outbox still guarantees
/// no event is lost; only the transport (and its retry/DLQ topology) is skipped.
/// </summary>
public sealed class InProcessOutboxPublisher(IServiceScopeFactory scopes) : IOutboxPublisher
{
    public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        var domainEvent = NotificationMessageProcessor.Deserialize(message.Type, System.Text.Encoding.UTF8.GetBytes(message.Payload));
        // Separate scope = separate DbContext/transaction from the dispatcher's batch transaction.
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NotificationMessageProcessor>().ProcessAsync(message.Id, domainEvent, ct);
    }
}
