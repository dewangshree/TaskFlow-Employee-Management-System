using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Messaging;

/// <summary>
/// Publishes outbox messages with publisher confirms and mandatory routing: the call
/// only returns after the broker has durably accepted the message, so the outbox row
/// is marked processed only when delivery to a queue is guaranteed.
/// </summary>
public sealed class RabbitMqPublisher(RabbitMqConnection connection, IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(ct);
            var properties = new BasicProperties
            {
                MessageId = message.Id.ToString(),
                Type = message.Type,
                CorrelationId = message.CorrelationId,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAt).ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?> { [MessageHeaders.CorrelationId] = message.CorrelationId },
            };

            await channel.BasicPublishAsync(
                options.Value.Exchange,
                message.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken: ct);
        }
        catch
        {
            // Force a fresh channel next time; a failed confirm can leave it unusable.
            await ResetChannelAsync();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        var conn = await connection.GetAsync(ct);
        _channel = await conn.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);
        await RabbitMqTopology.DeclareAsync(_channel, options.Value, ct);
        return _channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is null) return;
        try { await _channel.DisposeAsync(); } catch { /* channel already broken */ }
        _channel = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetChannelAsync();
        _lock.Dispose();
    }
}
