using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TaskFlow.Application.Caching;
using TaskFlow.Application.Events;
using TaskFlow.Application.Features.Notifications;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Messaging;

public enum ProcessingOutcome { Processed, Duplicate }

/// <summary>Thrown for messages that can never succeed (unknown type, bad JSON). Sent straight to the DLQ.</summary>
public sealed class PoisonMessageException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Idempotent consumer ("inbox" pattern). The ProcessedMessages insert and the
/// notifications are committed in one transaction, so a redelivered message is
/// detected and skipped instead of creating duplicate notifications.
/// </summary>
public sealed class NotificationMessageProcessor(
    AppDbContext db,
    INotificationEventHandler handler,
    ICacheService cache,
    TimeProvider time,
    ILogger<NotificationMessageProcessor> logger)
{
    public const string ConsumerName = "notifications";

    public static IDomainEvent Deserialize(string? type, ReadOnlySpan<byte> body)
    {
        var clrType = type is null ? null : IntegrationEventCatalog.ResolveType(type);
        if (clrType is null)
            throw new PoisonMessageException($"Unknown message type '{type}'.");

        try
        {
            return (IDomainEvent?)JsonSerializer.Deserialize(body, clrType, MessagingJson.Options)
                   ?? throw new PoisonMessageException("Message body deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new PoisonMessageException("Message body is not valid JSON for its type.", ex);
        }
    }

    public async Task<ProcessingOutcome> ProcessAsync(Guid messageId, IDomainEvent domainEvent, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId && p.Consumer == ConsumerName, ct))
        {
            logger.LogInformation("Skipping already processed message {MessageId}", messageId);
            return ProcessingOutcome.Duplicate;
        }

        var recipients = await handler.HandleAsync(domainEvent, ct);
        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId,
            Consumer = ConsumerName,
            ProcessedAt = time.GetUtcNow().UtcDateTime,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
                                           or Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 })
        {
            // Another worker instance processed the same message concurrently.
            logger.LogInformation("Concurrent duplicate detected for message {MessageId}", messageId);
            return ProcessingOutcome.Duplicate;
        }

        // Badge counts are cached per user; drop them so the new notification shows up on the next poll.
        await cache.RemoveAsync(recipients.Select(CacheKeys.UnreadNotificationCount), ct);
        logger.LogInformation("Created notifications for {RecipientCount} user(s) from {EventType} {MessageId}",
            recipients.Count, domainEvent.GetType().Name, messageId);
        return ProcessingOutcome.Processed;
    }
}
