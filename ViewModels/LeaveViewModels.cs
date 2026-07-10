using System.ComponentModel.DataAnnotations;
using TaskFlow.Models;

namespace TaskFlow.ViewModels;

public class LeaveRequestViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Leave Type")]
    public LeaveType LeaveType { get; set; } = LeaveType.Casual;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;

    [Required]
    [StringLength(1000)]
    [Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;

    public LeaveStatus Status { get; set; }
    public DateTime AppliedOn { get; set; }
    public string? EmployeeName { get; set; }
    public string? AdminComment { get; set; }
}

public class LeaveListViewModel
{
    public Helpers.PaginatedList<LeaveRequestViewModel> LeaveRequests { get; set; } = null!;
    public string? Search { get; set; }
    public LeaveStatus? StatusFilter { get; set; }
    public bool IsAdminView { get; set; }
}

public class LeaveActionViewModel
{
    public int Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Admin Comment")]
    public string? AdminComment { get; set; }
}
