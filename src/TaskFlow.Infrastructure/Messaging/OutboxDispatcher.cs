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
    IOutboxPublisher publisher,
    IOptions<OutboxOptions> options,
    TimeProvider time,
    ILogger<OutboxDispatcher> logger)
{
    public async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var now = time.GetUtcNow().UtcDateTime;

        // Postgres: row locks (FOR UPDATE SKIP LOCKED) need a transaction. SQLite allows a single
        // writer, so holding a transaction here would block the notification writes it triggers.
        await using var transaction = db.Database.IsNpgsql() ? await db.Database.BeginTransactionAsync(ct) : null;

        var batch = !db.Database.IsNpgsql()
            ? await db.OutboxMessages // SQLite: single instance, the DB write lock serialises access
                .Where(m => m.ProcessedAt == null && m.Attempts < opts.MaxAttempts && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
                .OrderBy(m => m.OccurredAt).Take(opts.BatchSize).ToListAsync(ct)
            : await db.OutboxMessages
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
        if (transaction is not null)
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
