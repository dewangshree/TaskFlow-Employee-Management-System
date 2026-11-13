using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
using TaskFlow.Infrastructure.Messaging;
using TaskFlow.Infrastructure.Observability;

namespace TaskFlow.Infrastructure.Messaging.Hosting;

/// <summary>
/// Consumes integration events and creates in-app notifications.
/// Manual acks + prefetch; failures go to delayed retry queues (5s/30s/2m), then to the DLQ.
/// Poison messages (unknown type / bad JSON) are rejected straight to the DLQ.
/// </summary>
public sealed class NotificationConsumerService(
    RabbitMqConnection connection,
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> options,
    ILogger<NotificationConsumerService> logger) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep trying until the broker is reachable (e.g. container start order).
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RabbitMQ unavailable; retrying consumer start in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
            }
        }

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
    }

    private async Task StartConsumingAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var conn = await connection.GetAsync(ct);
        _channel = await conn.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await RabbitMqTopology.DeclareAsync(_channel, opts, ct);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: opts.PrefetchCount, global: false, ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(_channel, delivery, ct);
        await _channel.BasicConsumeAsync(opts.NotificationsQueue, autoAck: false, consumer: consumer, cancellationToken: ct);
        logger.LogInformation("Consuming from {Queue}", opts.NotificationsQueue);
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        var props = delivery.BasicProperties;
        var correlationId = props.CorrelationId;
        CorrelationContext.CorrelationId = correlationId;
        using var _ = LogContext.PushProperty(CorrelationContext.LogPropertyName, correlationId);

        try
        {
            if (!Guid.TryParse(props.MessageId, out var messageId))
                throw new PoisonMessageException("Missing or invalid MessageId.");

            var domainEvent = NotificationMessageProcessor.Deserialize(props.Type, delivery.Body.Span);

            using var scope = scopes.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<NotificationMessageProcessor>();
            await processor.ProcessAsync(messageId, domainEvent, ct);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (PoisonMessageException ex)
        {
            logger.LogError(ex, "Poison message {MessageId} sent to dead-letter queue", props.MessageId);
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, ct); // DLX -> .dlq
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await RetryOrDeadLetterAsync(channel, delivery, ex, ct);
        }
    }

    private async Task RetryOrDeadLetterAsync(IChannel channel, BasicDeliverEventArgs delivery, Exception error, CancellationToken ct)
    {
        var opts = options.Value;
        var attempt = ReadRetryCount(delivery.BasicProperties) + 1;
        var exhausted = attempt > opts.RetryDelaysSeconds.Length;
        var target = exhausted ? opts.DeadLetterQueue : opts.RetryQueue(attempt);

        var headers = new Dictionary<string, object?>(delivery.BasicProperties.Headers ?? new Dictionary<string, object?>())
        {
            [MessageHeaders.RetryCount] = attempt,
            [MessageHeaders.LastError] = error.Message.Length > 500 ? error.Message[..500] : error.Message,
        };
        var props = new BasicProperties(delivery.BasicProperties) { Headers = headers };

        // Publish the copy (confirmed) before acking the original: a crash in between
        // causes a redelivery, which the idempotent processor handles safely.
        await channel.BasicPublishAsync("", target, mandatory: true, basicProperties: props, body: delivery.Body, cancellationToken: ct);
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);

        if (exhausted)
            logger.LogError(error, "Message {MessageId} failed after {Attempts} attempts; moved to {Queue}",
                delivery.BasicProperties.MessageId, attempt - 1, target);
        else
            logger.LogWarning(error, "Message {MessageId} failed; scheduled retry {Attempt} via {Queue}",
                delivery.BasicProperties.MessageId, attempt, target);
    }

    private static int ReadRetryCount(IReadOnlyBasicProperties props) =>
        props.Headers?.TryGetValue(MessageHeaders.RetryCount, out var value) == true
            ? value switch { int i => i, long l => (int)l, byte[] b => int.TryParse(Encoding.UTF8.GetString(b), out var p) ? p : 0, _ => 0 }
            : 0;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null) await _channel.DisposeAsync();
    }
}
