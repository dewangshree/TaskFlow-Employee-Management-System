namespace TaskFlow.ViewModels;

public class ProfileViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
    public string Role { get; set; } = string.Empty;
    public int AssignedTasksCount { get; set; }
    public int CompletedTasksCount { get; set; }
    public int PendingLeaveRequests { get; set; }
}
