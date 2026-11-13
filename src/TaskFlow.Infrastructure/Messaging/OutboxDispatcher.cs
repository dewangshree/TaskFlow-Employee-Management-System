using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Messaging;

/// <summary>
/// Moves events from the outbox table to RabbitMQ.
/// FOR UPDATE SKIP LOCKED lets several worker instances drain the outbox in
/// parallel without publishing the same row twice concurrently.
/// </summary>
public sealed class OutboxDispatcher(
    AppDbContext db,
    RabbitMqPublisher publisher,
    IOptions<OutboxOptions> options,
    TimeProvider time,
    ILogger<OutboxDispatcher> logger)
{
    public async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var now = time.GetUtcNow().UtcDateTime;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var batch = await db.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAt" IS NULL
                  AND "Attempts" < {opts.MaxAttempts}
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
                ORDER BY "OccurredAt"
                LIMIT {opts.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        if (batch.Count == 0)
            return 0;

        var published = 0;
        foreach (var message in batch)
        {
            try
            {
                await publisher.PublishAsync(message, ct);
                message.ProcessedAt = time.GetUtcNow().UtcDateTime;
                message.LastError = null;
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = Truncate(ex.Message, 2000);
                // Exponential back-off capped at 5 minutes.
                message.NextAttemptAt = now.AddSeconds(Math.Min(300, Math.Pow(2, message.Attempts)));
                logger.LogWarning(ex, "Failed to publish outbox message {MessageId} ({Type}), attempt {Attempt}",
                    message.Id, message.Type, message.Attempts);

                if (message.Attempts >= opts.MaxAttempts)
                    logger.LogError("Outbox message {MessageId} exceeded {MaxAttempts} attempts and is parked", message.Id, opts.MaxAttempts);
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        if (published > 0)
            logger.LogInformation("Published {Count} outbox message(s)", published);
        return published;
    }

    /// <summary>Deletes processed rows past the retention window to keep the table small.</summary>
    public Task<int> PurgeProcessedAsync(CancellationToken ct)
    {
        var cutoff = time.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays);
        return db.OutboxMessages.Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff).ExecuteDeleteAsync(ct);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
