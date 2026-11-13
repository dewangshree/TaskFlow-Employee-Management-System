using RabbitMQ.Client;
using TaskFlow.Application.Events;

namespace TaskFlow.Infrastructure.Messaging;

/// <summary>
/// Declares exchanges and queues (idempotent; both publisher and consumer call it).
///
///   taskflow.events (topic) --task.* / leave.*--> taskflow.notifications
///   taskflow.notifications            (rejects dead-letter to) taskflow.notifications.dlq
///   taskflow.notifications.retry.N    (TTL = delay N, then dead-letters back to the main queue)
///
/// One retry queue per attempt (5s, 30s, 2m) gives exponential back-off without the
/// head-of-line blocking that per-message TTLs cause in a single shared queue.
/// </summary>
public static class RabbitMqTopology
{
    public static async Task DeclareAsync(IChannel channel, RabbitMqOptions options, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        await channel.QueueDeclareAsync(options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);

        await channel.QueueDeclareAsync(options.NotificationsQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = options.DeadLetterQueue,
            },
            cancellationToken: ct);

        foreach (var routingKey in IntegrationEventCatalog.AllRoutingKeys)
            await channel.QueueBindAsync(options.NotificationsQueue, options.Exchange, routingKey, cancellationToken: ct);

        for (var attempt = 1; attempt <= options.RetryDelaysSeconds.Length; attempt++)
        {
            await channel.QueueDeclareAsync(options.RetryQueue(attempt), durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = options.RetryDelaysSeconds[attempt - 1] * 1000,
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = options.NotificationsQueue,
                },
                cancellationToken: ct);
        }
    }
}
