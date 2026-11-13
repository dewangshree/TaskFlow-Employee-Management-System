using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>
/// In-app notification created asynchronously by the background worker when it
/// consumes integration events from RabbitMQ.
/// </summary>
public class Notification
{
    private Notification() { } // EF Core

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public ApplicationUser? User { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    /// <summary>The event that produced this notification. Unique per user, which makes
    /// notification creation idempotent even if a message is delivered twice.</summary>
    public Guid SourceEventId { get; private set; }

    public static Notification Create(
        string userId, NotificationType type, string title, string message, string? link,
        Guid sourceEventId, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Type = type,
        Title = title,
        Message = message,
        Link = link,
        SourceEventId = sourceEventId,
        CreatedAt = nowUtc,
    };

    public void MarkRead(DateTime nowUtc)
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = nowUtc;
    }
}
