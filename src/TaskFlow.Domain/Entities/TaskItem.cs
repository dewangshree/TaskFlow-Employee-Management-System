using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public class TaskItem : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    /// <summary>
    /// Allowed status transitions. Same-status updates are treated as no-ops.
    /// Moving a task into or out of Cancelled is an administrative decision.
    /// </summary>
    private static readonly Dictionary<TaskItemStatus, TaskItemStatus[]> AllowedTransitions = new()
    {
        [TaskItemStatus.Pending] = [TaskItemStatus.InProgress, TaskItemStatus.Completed, TaskItemStatus.Cancelled],
        [TaskItemStatus.InProgress] = [TaskItemStatus.Pending, TaskItemStatus.Completed, TaskItemStatus.Cancelled],
        [TaskItemStatus.Completed] = [TaskItemStatus.InProgress],
        [TaskItemStatus.Cancelled] = [TaskItemStatus.Pending],
    };

    private TaskItem() { } // EF Core

    public int Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TaskPriority Priority { get; private set; } = TaskPriority.Medium;
    public TaskItemStatus Status { get; private set; } = TaskItemStatus.Pending;
    public DateOnly DueDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public string? AssignedToId { get; private set; }
    public ApplicationUser? AssignedTo { get; private set; }

    public string CreatedById { get; private set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; private set; }

    /// <summary>Optimistic concurrency token (mapped to PostgreSQL xmin).</summary>
    public uint Version { get; private set; }

    public static TaskItem Create(
        string title,
        string description,
        TaskPriority priority,
        DateOnly dueDate,
        string createdById,
        string? assigneeId,
        DateTime nowUtc)
    {
        var task = new TaskItem
        {
            Title = title.Trim(),
            Description = description.Trim(),
            Priority = priority,
            Status = TaskItemStatus.Pending,
            DueDate = dueDate,
            CreatedById = createdById,
            CreatedAt = nowUtc,
        };

        task.AssignTo(assigneeId, createdById, nowUtc);
        return task;
    }

    public void UpdateDetails(string title, string description, TaskPriority priority, DateOnly dueDate, DateTime nowUtc)
    {
        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        DueDate = dueDate;
        UpdatedAt = nowUtc;
    }

    public void AssignTo(string? assigneeId, string assignedById, DateTime nowUtc)
    {
        var normalized = string.IsNullOrWhiteSpace(assigneeId) ? null : assigneeId;
        if (normalized == AssignedToId)
            return;

        AssignedToId = normalized;
        UpdatedAt = nowUtc;

        if (normalized is not null)
            Raise(() => new TaskAssignedEvent(Id, Title, normalized, assignedById));
    }

    public bool CanTransitionTo(TaskItemStatus newStatus) =>
        AllowedTransitions.TryGetValue(Status, out var allowed) && allowed.Contains(newStatus);

    public static IReadOnlyCollection<TaskItemStatus> TransitionsFrom(TaskItemStatus status, bool isAdmin) =>
        AllowedTransitions[status]
            .Where(s => isAdmin || (s != TaskItemStatus.Cancelled && status != TaskItemStatus.Cancelled))
            .ToArray();

    public void ChangeStatus(TaskItemStatus newStatus, string changedById, bool changedByAdmin, DateTime nowUtc)
    {
        if (newStatus == Status)
            return;

        if (!changedByAdmin && (newStatus == TaskItemStatus.Cancelled || Status == TaskItemStatus.Cancelled))
            throw new DomainException("task.cancel_forbidden", "Only administrators can cancel or reinstate tasks.");

        if (!CanTransitionTo(newStatus))
            throw new DomainException(
                "task.invalid_transition",
                $"A task cannot move from {Status} to {newStatus}.");

        var oldStatus = Status;
        Status = newStatus;
        UpdatedAt = nowUtc;

        var assigneeId = AssignedToId;
        Raise(() => new TaskStatusChangedEvent(Id, Title, oldStatus, newStatus, changedById, assigneeId, CreatedById));
    }

    public bool IsOverdue(DateOnly today) =>
        DueDate < today && Status is TaskItemStatus.Pending or TaskItemStatus.InProgress;
}
