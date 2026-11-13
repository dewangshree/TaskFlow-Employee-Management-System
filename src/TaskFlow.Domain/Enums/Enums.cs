namespace TaskFlow.Domain.Enums;

public enum TaskPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum TaskItemStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled
}

public enum LeaveType
{
    Casual,
    Sick,
    Annual,
    Unpaid
}

public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected
}

public enum NotificationType
{
    TaskAssigned,
    TaskStatusChanged,
    LeaveRequested,
    LeaveReviewed
}
