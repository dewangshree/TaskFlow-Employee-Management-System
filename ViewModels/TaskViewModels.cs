using System.ComponentModel.DataAnnotations;
using TaskFlow.Models;

namespace TaskFlow.ViewModels;

public class TaskItemViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Priority")]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [Required]
    [Display(Name = "Status")]
    public Models.TaskStatus Status { get; set; } = Models.TaskStatus.Pending;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Due Date")]
    public DateTime DueDate { get; set; } = DateTime.UtcNow.Date.AddDays(7);

    [Display(Name = "Assign To")]
    public string? AssignedToId { get; set; }

    public string? AssignedToName { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TaskListViewModel
{
    public Helpers.PaginatedList<TaskItemViewModel> Tasks { get; set; } = null!;
    public string? Search { get; set; }
    public Models.TaskStatus? StatusFilter { get; set; }
    public TaskPriority? PriorityFilter { get; set; }
    public bool IsAdminView { get; set; }
}

public class UpdateTaskStatusViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Status")]
    public Models.TaskStatus Status { get; set; }

    public string Title { get; set; } = string.Empty;
}

public class AssignTaskViewModel
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Assign To")]
    public string AssignedToId { get; set; } = string.Empty;
}
