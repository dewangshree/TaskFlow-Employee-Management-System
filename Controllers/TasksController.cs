using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Helpers;
using TaskFlow.Models;
using TaskFlow.ViewModels;

namespace TaskFlow.Controllers;

[Authorize]
public class TasksController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TasksController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Index(string? search, Models.TaskStatus? statusFilter, TaskPriority? priorityFilter, int page = 1)
    {
        var query = _context.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                t.Title.Contains(search) ||
                t.Description.Contains(search) ||
                (t.AssignedTo != null && (t.AssignedTo.FirstName + " " + t.AssignedTo.LastName).Contains(search)));
        }

        if (statusFilter.HasValue)
            query = query.Where(t => t.Status == statusFilter.Value);

        if (priorityFilter.HasValue)
            query = query.Where(t => t.Priority == priorityFilter.Value);

        query = query.OrderByDescending(t => t.CreatedAt);

        var projected = query.Select(t => new TaskItemViewModel
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Priority = t.Priority,
            Status = t.Status,
            DueDate = t.DueDate,
            AssignedToId = t.AssignedToId,
            AssignedToName = t.AssignedTo != null ? t.AssignedTo.FirstName + " " + t.AssignedTo.LastName : "Unassigned",
            CreatedByName = t.CreatedBy != null ? t.CreatedBy.FirstName + " " + t.CreatedBy.LastName : string.Empty,
            CreatedAt = t.CreatedAt
        });

        var tasks = await PaginatedList<TaskItemViewModel>.CreateAsync(projected, page, 10);

        return View(new TaskListViewModel
        {
            Tasks = tasks,
            Search = search,
            StatusFilter = statusFilter,
            PriorityFilter = priorityFilter,
            IsAdminView = true
        });
    }

    [Authorize(Roles = RoleNames.Employee)]
    public async Task<IActionResult> MyTasks(string? search, Models.TaskStatus? statusFilter, TaskPriority? priorityFilter, int page = 1)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var query = _context.TaskItems
            .Include(t => t.CreatedBy)
            .Where(t => t.AssignedToId == user.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Title.Contains(search) || t.Description.Contains(search));

        if (statusFilter.HasValue)
            query = query.Where(t => t.Status == statusFilter.Value);

        if (priorityFilter.HasValue)
            query = query.Where(t => t.Priority == priorityFilter.Value);

        query = query.OrderByDescending(t => t.DueDate);

        var projected = query.Select(t => new TaskItemViewModel
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Priority = t.Priority,
            Status = t.Status,
            DueDate = t.DueDate,
            CreatedByName = t.CreatedBy != null ? t.CreatedBy.FirstName + " " + t.CreatedBy.LastName : string.Empty,
            CreatedAt = t.CreatedAt
        });

        var tasks = await PaginatedList<TaskItemViewModel>.CreateAsync(projected, page, 10);

        return View(new TaskListViewModel
        {
            Tasks = tasks,
            Search = search,
            StatusFilter = statusFilter,
            PriorityFilter = priorityFilter,
            IsAdminView = false
        });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateEmployeesDropdownAsync();
        return View(new TaskItemViewModel());
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaskItemViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateEmployeesDropdownAsync();
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var task = new TaskItem
        {
            Title = model.Title,
            Description = model.Description,
            Priority = model.Priority,
            Status = model.Status,
            DueDate = model.DueDate,
            AssignedToId = string.IsNullOrWhiteSpace(model.AssignedToId) ? null : model.AssignedToId,
            CreatedById = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Task created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var task = await _context.TaskItems.FindAsync(id);
        if (task == null)
            return NotFound();

        await PopulateEmployeesDropdownAsync();

        return View(new TaskItemViewModel
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Status = task.Status,
            DueDate = task.DueDate,
            AssignedToId = task.AssignedToId
        });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaskItemViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (!ModelState.IsValid)
        {
            await PopulateEmployeesDropdownAsync();
            return View(model);
        }

        var task = await _context.TaskItems.FindAsync(id);
        if (task == null)
            return NotFound();

        task.Title = model.Title;
        task.Description = model.Description;
        task.Priority = model.Priority;
        task.Status = model.Status;
        task.DueDate = model.DueDate;
        task.AssignedToId = string.IsNullOrWhiteSpace(model.AssignedToId) ? null : model.AssignedToId;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Task updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await _context.TaskItems
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        return View(new TaskItemViewModel
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Status = task.Status,
            DueDate = task.DueDate,
            AssignedToName = task.AssignedTo?.FullName ?? "Unassigned"
        });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var task = await _context.TaskItems.FindAsync(id);
        if (task == null)
            return NotFound();

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Task deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet]
    public async Task<IActionResult> Assign(int id)
    {
        var task = await _context.TaskItems.FindAsync(id);
        if (task == null)
            return NotFound();

        await PopulateEmployeesDropdownAsync();

        return View(new AssignTaskViewModel
        {
            TaskId = task.Id,
            TaskTitle = task.Title,
            AssignedToId = task.AssignedToId ?? string.Empty
        });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(AssignTaskViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateEmployeesDropdownAsync();
            return View(model);
        }

        var task = await _context.TaskItems.FindAsync(model.TaskId);
        if (task == null)
            return NotFound();

        task.AssignedToId = model.AssignedToId;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Task assigned successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public async Task<IActionResult> UpdateStatus(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.AssignedToId == user.Id);
        if (task == null)
            return NotFound();

        return View(new UpdateTaskStatusViewModel
        {
            Id = task.Id,
            Title = task.Title,
            Status = task.Status
        });
    }

    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(UpdateTaskStatusViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == model.Id && t.AssignedToId == user.Id);
        if (task == null)
            return NotFound();

        if (model.Status == Models.TaskStatus.Cancelled)
        {
            ModelState.AddModelError(nameof(model.Status), "Employees cannot cancel tasks.");
            return View(model);
        }

        task.Status = model.Status;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Task status updated successfully.";
        return RedirectToAction(nameof(MyTasks));
    }

    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Details(int id)
    {
        var task = await _context.TaskItems
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound();

        return View(new TaskItemViewModel
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Status = task.Status,
            DueDate = task.DueDate,
            AssignedToName = task.AssignedTo?.FullName ?? "Unassigned",
            CreatedByName = task.CreatedBy?.FullName ?? string.Empty,
            CreatedAt = task.CreatedAt
        });
    }

    private async Task PopulateEmployeesDropdownAsync()
    {
        var employees = await _userManager.GetUsersInRoleAsync(RoleNames.Employee);
        ViewBag.Employees = new SelectList(
            employees.Where(e => e.IsActive).OrderBy(e => e.FirstName),
            "Id",
            "FullName");
    }
}
