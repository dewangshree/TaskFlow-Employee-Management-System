using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Helpers;
using TaskFlow.Models;
using TaskFlow.ViewModels;

namespace TaskFlow.Controllers;

[Authorize]
public class LeavesController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public LeavesController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public IActionResult Apply()
    {
        return View(new LeaveRequestViewModel());
    }

    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(LeaveRequestViewModel model)
    {
        if (model.EndDate < model.StartDate)
            ModelState.AddModelError(nameof(model.EndDate), "End date must be on or after start date.");

        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var leave = new LeaveRequest
        {
            EmployeeId = user.Id,
            LeaveType = model.LeaveType,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            Reason = model.Reason,
            Status = LeaveStatus.Pending,
            AppliedOn = DateTime.UtcNow
        };

        _context.LeaveRequests.Add(leave);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request submitted successfully.";
        return RedirectToAction(nameof(History));
    }

    [Authorize(Roles = RoleNames.Employee)]
    public async Task<IActionResult> History(string? search, LeaveStatus? statusFilter, int page = 1)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return RedirectToAction("Login", "Account");

        var query = _context.LeaveRequests
            .Where(l => l.EmployeeId == user.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l => l.Reason.Contains(search));

        if (statusFilter.HasValue)
            query = query.Where(l => l.Status == statusFilter.Value);

        query = query.OrderByDescending(l => l.AppliedOn);

        var projected = query.Select(l => new LeaveRequestViewModel
        {
            Id = l.Id,
            LeaveType = l.LeaveType,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            Reason = l.Reason,
            Status = l.Status,
            AppliedOn = l.AppliedOn,
            AdminComment = l.AdminComment
        });

        var leaves = await PaginatedList<LeaveRequestViewModel>.CreateAsync(projected, page, 10);

        return View(new LeaveListViewModel
        {
            LeaveRequests = leaves,
            Search = search,
            StatusFilter = statusFilter,
            IsAdminView = false
        });
    }

    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Manage(string? search, LeaveStatus? statusFilter, int page = 1)
    {
        var query = _context.LeaveRequests
            .Include(l => l.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l =>
                l.Reason.Contains(search) ||
                (l.Employee != null && (l.Employee.FirstName + " " + l.Employee.LastName).Contains(search)));
        }

        if (statusFilter.HasValue)
            query = query.Where(l => l.Status == statusFilter.Value);

        query = query.OrderByDescending(l => l.AppliedOn);

        var projected = query.Select(l => new LeaveRequestViewModel
        {
            Id = l.Id,
            LeaveType = l.LeaveType,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            Reason = l.Reason,
            Status = l.Status,
            AppliedOn = l.AppliedOn,
            EmployeeName = l.Employee != null ? l.Employee.FirstName + " " + l.Employee.LastName : string.Empty,
            AdminComment = l.AdminComment
        });

        var leaves = await PaginatedList<LeaveRequestViewModel>.CreateAsync(projected, page, 10);

        return View(new LeaveListViewModel
        {
            LeaveRequests = leaves,
            Search = search,
            StatusFilter = statusFilter,
            IsAdminView = true
        });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet]
    public async Task<IActionResult> Review(int id)
    {
        var leave = await _context.LeaveRequests
            .Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (leave == null)
            return NotFound();

        return View(new LeaveActionViewModel
        {
            Id = leave.Id,
            EmployeeName = leave.Employee?.FullName ?? string.Empty,
            LeaveType = leave.LeaveType,
            StartDate = leave.StartDate,
            EndDate = leave.EndDate,
            Reason = leave.Reason,
            AdminComment = leave.AdminComment
        });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(LeaveActionViewModel model)
    {
        var leave = await _context.LeaveRequests.FindAsync(model.Id);
        if (leave == null)
            return NotFound();

        leave.Status = LeaveStatus.Approved;
        leave.AdminComment = model.AdminComment;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request approved.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(LeaveActionViewModel model)
    {
        var leave = await _context.LeaveRequests.FindAsync(model.Id);
        if (leave == null)
            return NotFound();

        leave.Status = LeaveStatus.Rejected;
        leave.AdminComment = model.AdminComment;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Leave request rejected.";
        return RedirectToAction(nameof(Manage));
    }
}
