using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Events;

namespace TaskFlow.Application.Features.Notifications;

/// <summary>
/// Turns integration events into in-app notifications. Runs inside the background
/// worker; it only stages entities on the DbContext. The caller commits them in the
/// same transaction as the idempotency (processed message) record.
/// </summary>
public interface INotificationEventHandler
{
    /// <returns>The user ids that received a notification (used for cache invalidation).</returns>
    Task<IReadOnlyCollection<string>> HandleAsync(IDomainEvent domainEvent, CancellationToken ct);
}

public sealed class NotificationEventHandler(IApplicationDbContext db, TimeProvider time) : INotificationEventHandler
{
    public Task<IReadOnlyCollection<string>> HandleAsync(IDomainEvent domainEvent, CancellationToken ct) =>
        domainEvent switch
        {
            TaskAssignedEvent e => OnTaskAssigned(e, ct),
            TaskStatusChangedEvent e => OnTaskStatusChanged(e, ct),
            LeaveRequestedEvent e => OnLeaveRequested(e, ct),
            LeaveReviewedEvent e => OnLeaveReviewed(e, ct),
            _ => Task.FromResult<IReadOnlyCollection<string>>([]),
        };

    private async Task<IReadOnlyCollection<string>> OnTaskAssigned(TaskAssignedEvent e, CancellationToken ct)
    {
        if (e.AssigneeId == e.AssignedById)
            return [];

        var assignerName = await NameOf(e.AssignedById, ct);
        Add(e.AssigneeId, NotificationType.TaskAssigned, "New task assigned",
            $"{assignerName} assigned you \"{e.TaskTitle}\".", $"/tasks/{e.TaskId}", e.EventId);
        return [e.AssigneeId];
    }

    private async Task<IReadOnlyCollection<string>> OnTaskStatusChanged(TaskStatusChangedEvent e, CancellationToken ct)
    {
        // Notify the "other side": the creator when an employee moves the task,
        // the assignee when an administrator changes it.
        var recipients = new[] { e.CreatedById, e.AssigneeId }
            .Where(id => id is not null && id != e.ChangedById)
            .Select(id => id!)
            .Distinct()
            .ToArray();

        if (recipients.Length == 0)
            return [];

        var actorName = await NameOf(e.ChangedById, ct);
        foreach (var recipient in recipients)
        {
            Add(recipient, NotificationType.TaskStatusChanged, "Task status updated",
                $"{actorName} moved \"{e.TaskTitle}\" from {Humanize(e.OldStatus)} to {Humanize(e.NewStatus)}.",
                $"/tasks/{e.TaskId}", e.EventId);
        }

        return recipients;
    }

    private async Task<IReadOnlyCollection<string>> OnLeaveRequested(LeaveRequestedEvent e, CancellationToken ct)
    {
        var admins = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && db.UserRoles.Any(ur => ur.UserId == u.Id &&
                db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMIN")))
            .Select(u => u.Id)
            .ToListAsync(ct);

        var employeeName = await NameOf(e.EmployeeId, ct);
        foreach (var adminId in admins)
        {
            Add(adminId, NotificationType.LeaveRequested, "Leave request awaiting review",
                $"{employeeName} requested {e.LeaveType.ToString().ToLowerInvariant()} leave ({FormatRange(e.StartDate, e.EndDate)}).",
                "/leaves/manage", e.EventId);
        }

        return admins;
    }

    private Task<IReadOnlyCollection<string>> OnLeaveReviewed(LeaveReviewedEvent e, CancellationToken ct)
    {
        var verb = e.Decision == LeaveStatus.Approved ? "approved" : "rejected";
        var message = $"Your leave for {FormatRange(e.StartDate, e.EndDate)} was {verb}."
            + (string.IsNullOrWhiteSpace(e.AdminComment) ? string.Empty : $" Comment: {e.AdminComment}");

        Add(e.EmployeeId, NotificationType.LeaveReviewed, $"Leave request {verb}", message, "/leaves", e.EventId);
        return Task.FromResult<IReadOnlyCollection<string>>([e.EmployeeId]);
    }

    private void Add(string userId, NotificationType type, string title, string message, string link, Guid eventId) =>
        db.Notifications.Add(Notification.Create(userId, type, title, message, link, eventId, time.UtcNow()));

    private async Task<string> NameOf(string userId, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync(ct) ?? "Someone";

    private static string Humanize(TaskItemStatus status) => status == TaskItemStatus.InProgress ? "In Progress" : status.ToString();

    private static string FormatRange(DateOnly start, DateOnly end) =>
        start == end ? start.ToString("MMM d, yyyy") : $"{start:MMM d} – {end:MMM d, yyyy}";
}
