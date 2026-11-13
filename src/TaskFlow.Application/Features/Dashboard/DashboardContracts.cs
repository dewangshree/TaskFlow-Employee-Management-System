using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Dashboard;

public sealed record TaskStatsDto(int Total, int Pending, int InProgress, int Completed, int Cancelled, int Overdue)
{
    public int Open => Pending + InProgress;
}

public sealed record LeaveStatsDto(int Pending, int Approved, int Rejected);

public sealed record RecentTaskDto(
    int Id, string Title, TaskPriority Priority, TaskItemStatus Status, DateOnly DueDate, bool IsOverdue, string? AssigneeName);

public sealed record RecentLeaveDto(
    int Id, string EmployeeName, LeaveType LeaveType, DateOnly StartDate, DateOnly EndDate, LeaveStatus Status, DateTime AppliedOn);

public sealed record AdminDashboardDto(
    int TotalEmployees,
    int ActiveEmployees,
    int EmployeesOnLeaveToday,
    TaskStatsDto Tasks,
    LeaveStatsDto Leaves,
    IReadOnlyList<RecentTaskDto> RecentTasks,
    IReadOnlyList<RecentLeaveDto> RecentLeaves,
    DateTime GeneratedAt);

public sealed record EmployeeDashboardDto(
    TaskStatsDto Tasks,
    LeaveStatsDto Leaves,
    int ApprovedLeaveDaysThisYear,
    IReadOnlyList<RecentTaskDto> UpcomingTasks,
    IReadOnlyList<RecentLeaveDto> RecentLeaves,
    DateTime GeneratedAt);
