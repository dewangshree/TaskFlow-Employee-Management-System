using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Models;

public class TaskItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public Models.TaskStatus Status { get; set; } = Models.TaskStatus.Pending;

    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; } = DateTime.UtcNow.Date.AddDays(7);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    [Required]
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }
}
