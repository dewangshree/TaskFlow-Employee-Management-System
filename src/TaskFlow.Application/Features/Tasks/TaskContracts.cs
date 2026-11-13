using TaskFlow.Application.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Tasks;

public sealed record TaskListItemDto(
    int Id,
    string Title,
    TaskPriority Priority,
    TaskItemStatus Status,
    DateOnly DueDate,
    bool IsOverdue,
    string? AssigneeId,
    string? AssigneeName,
    string CreatedByName,
    DateTime CreatedAt,
    uint Version);

public sealed record TaskDetailDto(
    int Id,
    string Title,
    string Description,
    TaskPriority Priority,
    TaskItemStatus Status,
    DateOnly DueDate,
    bool IsOverdue,
    string? AssigneeId,
    string? AssigneeName,
    string CreatedById,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    uint Version,
    IReadOnlyCollection<TaskItemStatus> AllowedTransitions);

public sealed record TaskQuery : PagedQuery
{
    public TaskItemStatus? Status { get; init; }
    public TaskPriority? Priority { get; init; }
    public string? AssigneeId { get; init; }
    public bool? OverdueOnly { get; init; }
}

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    DateOnly DueDate,
    string? AssigneeId);

public sealed record UpdateTaskRequest(
    string Title,
    string? Description,
    TaskPriority Priority,
    TaskItemStatus Status,
    DateOnly DueDate,
    string? AssigneeId,
    uint Version);

public sealed record AssignTaskRequest(string? AssigneeId, uint Version);

public sealed record UpdateTaskStatusRequest(TaskItemStatus Status, uint Version);
