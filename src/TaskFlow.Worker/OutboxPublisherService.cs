using Microsoft.Extensions.Options;
using TaskFlow.Infrastructure.Messaging;

namespace TaskFlow.Worker;

/// <summary>Polls the transactional outbox and publishes pending events to RabbitMQ.</summary>
public sealed class OutboxPublisherService(IServiceScopeFactory scopes, IOptions<OutboxOptions> options, ILogger<OutboxPublisherService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(options.Value.PollingIntervalMilliseconds);
        var lastPurge = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

                // Drain quickly while there is a backlog, then fall back to polling.
                int published;
                do { published = await dispatcher.DispatchBatchAsync(stoppingToken); }
                while (published >= options.Value.BatchSize && !stoppingToken.IsCancellationRequested);

                if (DateTime.UtcNow - lastPurge > TimeSpan.FromHours(1))
                {
                    var purged = await dispatcher.PurgeProcessedAsync(stoppingToken);
                    if (purged > 0) logger.LogInformation("Purged {Count} processed outbox messages", purged);
                    lastPurge = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch cycle failed; retrying");
            }

            await Task.Delay(interval, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }
    }
}
