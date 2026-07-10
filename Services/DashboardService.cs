using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Helpers;
using TaskFlow.Models;

namespace TaskFlow.Services;

public class DashboardService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardService(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<ViewModels.DashboardViewModel> GetAdminDashboardAsync()
    {
        var employees = await _userManager.GetUsersInRoleAsync(RoleNames.Employee);

        return new ViewModels.DashboardViewModel
        {
            IsAdmin = true,
            TotalEmployees = employees.Count,
            TotalTasks = await _context.TaskItems.CountAsync(),
            PendingTasks = await _context.TaskItems.CountAsync(t => t.Status == Models.TaskStatus.Pending || t.Status == Models.TaskStatus.InProgress),
            CompletedTasks = await _context.TaskItems.CountAsync(t => t.Status == Models.TaskStatus.Completed),
            PendingLeaveRequests = await _context.LeaveRequests.CountAsync(l => l.Status == LeaveStatus.Pending),
            ApprovedLeaveRequests = await _context.LeaveRequests.CountAsync(l => l.Status == LeaveStatus.Approved),
            RecentTasks = await GetRecentTasksAsync(null),
            RecentLeaveRequests = await GetRecentLeaveRequestsAsync(null)
        };
    }

    public async Task<ViewModels.DashboardViewModel> GetEmployeeDashboardAsync(string userId, string userName)
    {
        var myTasks = _context.TaskItems.Where(t => t.AssignedToId == userId);

        return new ViewModels.DashboardViewModel
        {
            IsAdmin = false,
            UserName = userName,
            TotalTasks = await myTasks.CountAsync(),
            PendingTasks = await myTasks.CountAsync(t => t.Status == Models.TaskStatus.Pending || t.Status == Models.TaskStatus.InProgress),
            CompletedTasks = await myTasks.CountAsync(t => t.Status == Models.TaskStatus.Completed),
            PendingLeaveRequests = await _context.LeaveRequests.CountAsync(l => l.EmployeeId == userId && l.Status == LeaveStatus.Pending),
            ApprovedLeaveRequests = await _context.LeaveRequests.CountAsync(l => l.EmployeeId == userId && l.Status == LeaveStatus.Approved),
            RecentTasks = await GetRecentTasksAsync(userId),
            RecentLeaveRequests = await GetRecentLeaveRequestsAsync(userId)
        };
    }

    private async Task<List<ViewModels.TaskItemViewModel>> GetRecentTasksAsync(string? userId)
    {
        var query = _context.TaskItems
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (userId != null)
            query = query.Where(t => t.AssignedToId == userId);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new ViewModels.TaskItemViewModel
            {
                Id = t.Id,
                Title = t.Title,
                Priority = t.Priority,
                Status = t.Status,
                DueDate = t.DueDate,
                AssignedToName = t.AssignedTo != null ? t.AssignedTo.FirstName + " " + t.AssignedTo.LastName : "Unassigned"
            })
            .ToListAsync();
    }

    private async Task<List<ViewModels.LeaveRequestViewModel>> GetRecentLeaveRequestsAsync(string? userId)
    {
        var query = _context.LeaveRequests
            .Include(l => l.Employee)
            .AsQueryable();

        if (userId != null)
            query = query.Where(l => l.EmployeeId == userId);

        return await query
            .OrderByDescending(l => l.AppliedOn)
            .Take(5)
            .Select(l => new ViewModels.LeaveRequestViewModel
            {
                Id = l.Id,
                LeaveType = l.LeaveType,
                StartDate = l.StartDate,
                EndDate = l.EndDate,
                Status = l.Status,
                AppliedOn = l.AppliedOn,
                EmployeeName = l.Employee != null ? l.Employee.FirstName + " " + l.Employee.LastName : string.Empty
            })
            .ToListAsync();
    }
}
