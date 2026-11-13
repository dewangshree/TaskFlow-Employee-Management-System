using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Events;

public sealed record TaskAssignedEvent(
    int TaskId,
    string TaskTitle,
    string AssigneeId,
    string AssignedById) : DomainEvent;

public sealed record TaskStatusChangedEvent(
    int TaskId,
    string TaskTitle,
    TaskItemStatus OldStatus,
    TaskItemStatus NewStatus,
    string ChangedById,
    string? AssigneeId,
    string CreatedById) : DomainEvent;
