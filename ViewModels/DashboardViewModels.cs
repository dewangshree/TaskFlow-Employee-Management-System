namespace TaskFlow.ViewModels;

public class DashboardViewModel
{
    public int TotalEmployees { get; set; }
    public int TotalTasks { get; set; }
    public int PendingTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingLeaveRequests { get; set; }
    public int ApprovedLeaveRequests { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public List<TaskItemViewModel> RecentTasks { get; set; } = new();
    public List<LeaveRequestViewModel> RecentLeaveRequests { get; set; } = new();
}

public class EmployeeListViewModel
{
    public Helpers.PaginatedList<EmployeeViewModel> Employees { get; set; } = null!;
    public string? Search { get; set; }
}

public class EmployeeViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
    public bool IsActive { get; set; }
    public int TaskCount { get; set; }
}
