using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Helpers;
using TaskFlow.Models;
using TaskFlow.ViewModels;

namespace TaskFlow.Controllers;

[Authorize(Roles = RoleNames.Admin)]
public class EmployeesController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;

    public EmployeesController(UserManager<ApplicationUser> userManager, AppDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var employees = await _userManager.GetUsersInRoleAsync(RoleNames.Employee);
        var query = employees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                e.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (e.Email != null && e.Email.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                e.Department.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();

        var employeeIds = ordered.Select(e => e.Id).ToList();
        var taskCounts = await _context.TaskItems
            .Where(t => t.AssignedToId != null && employeeIds.Contains(t.AssignedToId))
            .GroupBy(t => t.AssignedToId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId!, x => x.Count);

        var viewModels = ordered.Select(e => new EmployeeViewModel
        {
            Id = e.Id,
            FullName = e.FullName,
            Email = e.Email ?? string.Empty,
            Department = e.Department,
            JoinDate = e.JoinDate,
            IsActive = e.IsActive,
            TaskCount = taskCounts.GetValueOrDefault(e.Id, 0)
        }).ToList();

        var count = viewModels.Count;
        var items = viewModels.Skip((page - 1) * 10).Take(10).ToList();
        var paginated = new PaginatedList<EmployeeViewModel>(items, count, page, 10);

        return View(new EmployeeListViewModel
        {
            Employees = paginated,
            Search = search
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var isEmployee = await _userManager.IsInRoleAsync(user, RoleNames.Employee);
        if (!isEmployee)
        {
            TempData["Error"] = "Only employee accounts can be managed here.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        TempData["Success"] = user.IsActive
            ? $"{user.FullName} has been activated."
            : $"{user.FullName} has been deactivated.";

        return RedirectToAction(nameof(Index));
    }
}
