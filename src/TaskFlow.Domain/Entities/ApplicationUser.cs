using Microsoft.AspNetCore.Identity;

namespace TaskFlow.Domain.Entities;

/// <summary>
/// Identity user extended with HR attributes. Inherits from IdentityUser as a
/// deliberate, pragmatic coupling: the app uses ASP.NET Core Identity as its
/// user store and duplicating the user model would add mapping without value.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}";

    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}
