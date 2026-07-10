using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Models;

public class LeaveRequest
{
    public int Id { get; set; }

    [Required]
    public string EmployeeId { get; set; } = string.Empty;
    public ApplicationUser? Employee { get; set; }

    public LeaveType LeaveType { get; set; } = LeaveType.Casual;

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public DateTime AppliedOn { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? AdminComment { get; set; }
}
