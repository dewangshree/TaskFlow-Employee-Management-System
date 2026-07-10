using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Models;

public enum TaskPriority
{
    [Display(Name = "Low")]
    Low,
    [Display(Name = "Medium")]
    Medium,
    [Display(Name = "High")]
    High,
    [Display(Name = "Critical")]
    Critical
}

public enum TaskStatus
{
    [Display(Name = "Pending")]
    Pending,
    [Display(Name = "In Progress")]
    InProgress,
    [Display(Name = "Completed")]
    Completed,
    [Display(Name = "Cancelled")]
    Cancelled
}

public enum LeaveType
{
    [Display(Name = "Casual")]
    Casual,
    [Display(Name = "Sick")]
    Sick,
    [Display(Name = "Annual")]
    Annual,
    [Display(Name = "Unpaid")]
    Unpaid
}

public enum LeaveStatus
{
    [Display(Name = "Pending")]
    Pending,
    [Display(Name = "Approved")]
    Approved,
    [Display(Name = "Rejected")]
    Rejected
}
