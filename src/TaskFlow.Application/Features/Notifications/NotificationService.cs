using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Caching;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Notifications;

public sealed record NotificationDto(
    Guid Id, NotificationType Type, string Title, string Message, string? Link, bool IsRead, DateTime CreatedAt);

public sealed record NotificationQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public bool UnreadOnly { get; init; }
}

public sealed record UnreadCountDto(int Count);

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> ListAsync(NotificationQuery query, CancellationToken ct);
    Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken ct);
    Task MarkReadAsync(Guid id, CancellationToken ct);
    Task MarkAllReadAsync(CancellationToken ct);
}

public sealed class NotificationService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICacheService cache,
    IOptions<CacheOptions> cacheOptions,
    TimeProvider time) : INotificationService
{
    public Task<PagedResult<NotificationDto>> ListAsync(NotificationQuery query, CancellationToken ct)
    {
        var userId = currentUser.RequiredUserId;
        var notifications = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (query.UnreadOnly)
            notifications = notifications.Where(n => !n.IsRead);

        return notifications
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.Link, n.IsRead, n.CreatedAt))
            .ToPagedResultAsync(Math.Max(1, query.Page), Math.Clamp(query.PageSize, 1, 50), ct);
    }

    /// <summary>
    /// Polled by every open browser tab, so the count is cached per user and
    /// invalidated by the worker when it creates a notification.
    /// </summary>
    public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken ct)
    {
        var userId = currentUser.RequiredUserId;
        var count = await cache.GetOrCreateAsync(
            CacheKeys.UnreadNotificationCount(userId),
            token => db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, token),
            cacheOptions.Value.NotificationCountTtl,
            ct);
        return new UnreadCountDto(count);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequiredUserId;
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct)
            ?? throw new NotFoundException("Notification", id);

        notification.MarkRead(time.UtcNow());
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.UnreadNotificationCount(userId));
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var userId = currentUser.RequiredUserId;
        var now = time.UtcNow();
        // Set-based UPDATE: one statement regardless of how many rows are unread.
        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, now), ct);
        await cache.RemoveAsync(CacheKeys.UnreadNotificationCount(userId));
    }
}
