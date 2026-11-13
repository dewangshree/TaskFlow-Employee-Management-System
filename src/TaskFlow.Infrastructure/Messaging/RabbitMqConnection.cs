using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace TaskFlow.Infrastructure.Messaging;

/// <summary>
/// One long-lived AMQP connection per process (connections are expensive; channels are cheap).
/// Automatic recovery re-establishes the connection and topology after broker restarts.
/// </summary>
public sealed class RabbitMqConnection(string connectionString, ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                ClientProvidedName = $"taskflow-{AppDomain.CurrentDomain.FriendlyName}",
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            };

            _connection = await factory.CreateConnectionAsync(ct);
            logger.LogInformation("Connected to RabbitMQ at {Host}", factory.HostName);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
