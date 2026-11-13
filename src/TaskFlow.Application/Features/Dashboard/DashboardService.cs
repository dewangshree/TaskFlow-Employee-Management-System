using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Caching;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Employees;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Dashboard;

public interface IDashboardService
{
    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct);
    Task<EmployeeDashboardDto> GetEmployeeDashboardAsync(string userId, CancellationToken ct);
}

/// <summary>
/// Dashboards are the most frequently loaded screens and each one runs several
/// aggregate queries. They are served cache-aside from Redis (short TTL plus
/// explicit invalidation on writes), falling back to PostgreSQL if Redis is down.
/// </summary>
public sealed class DashboardService(
    IApplicationDbContext db,
    IEmployeeDirectory directory,
    ICacheService cache,
    IOptions<CacheOptions> cacheOptions,
    TimeProvider time) : IDashboardService
{
    private const int RecentItems = 5;

    public Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct) =>
        cache.GetOrCreateAsync(CacheKeys.AdminDashboard, BuildAdminDashboardAsync, cacheOptions.Value.DashboardTtl, ct);

    public Task<EmployeeDashboardDto> GetEmployeeDashboardAsync(string userId, CancellationToken ct) =>
        cache.GetOrCreateAsync(
            CacheKeys.EmployeeDashboard(userId),
            token => BuildEmployeeDashboardAsync(userId, token),
            cacheOptions.Value.DashboardTtl,
            ct);

    private async Task<AdminDashboardDto> BuildAdminDashboardAsync(CancellationToken ct)
    {
        var today = time.Today();

        // One GROUP BY per aggregate instead of a COUNT query per status.
        var employeeCounts = await directory.EmployeesQuery()
            .GroupBy(u => u.IsActive)
            .Select(g => new { IsActive = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var tasks = await GetTaskStatsAsync(db.TaskItems.AsNoTracking(), today, ct);
        var leaves = await GetLeaveStatsAsync(db.LeaveRequests.AsNoTracking(), ct);

        var onLeaveToday = await db.LeaveRequests.AsNoTracking()
            .Where(l => l.Status == LeaveStatus.Approved && l.StartDate <= today && l.EndDate >= today)
            .Select(l => l.EmployeeId)
            .Distinct()
            .CountAsync(ct);

        var recentTasks = await ProjectRecentTasks(db.TaskItems.AsNoTracking().OrderByDescending(t => t.CreatedAt), today)
            .Take(RecentItems).ToListAsync(ct);

        var recentLeaves = await ProjectRecentLeaves(db.LeaveRequests.AsNoTracking().OrderByDescending(l => l.AppliedOn))
            .Take(RecentItems).ToListAsync(ct);

        return new AdminDashboardDto(
            employeeCounts.Sum(c => c.Count),
            employeeCounts.Where(c => c.IsActive).Sum(c => c.Count),
            onLeaveToday,
            tasks,
            leaves,
            recentTasks,
            recentLeaves,
            time.UtcNow());
    }

    private async Task<EmployeeDashboardDto> BuildEmployeeDashboardAsync(string userId, CancellationToken ct)
    {
        var today = time.Today();
        var myTasks = db.TaskItems.AsNoTracking().Where(t => t.AssignedToId == userId);
        var myLeaves = db.LeaveRequests.AsNoTracking().Where(l => l.EmployeeId == userId);

        var tasks = await GetTaskStatsAsync(myTasks, today, ct);
        var leaves = await GetLeaveStatsAsync(myLeaves, ct);

        var yearStart = new DateOnly(today.Year, 1, 1);
        var yearEnd = new DateOnly(today.Year, 12, 31);
        var approvedThisYear = await myLeaves
            .Where(l => l.Status == LeaveStatus.Approved && l.EndDate >= yearStart && l.StartDate <= yearEnd)
            .Select(l => new { l.StartDate, l.EndDate })
            .ToListAsync(ct);
        var approvedDays = approvedThisYear.Sum(l =>
            (l.EndDate > yearEnd ? yearEnd : l.EndDate).DayNumber - (l.StartDate < yearStart ? yearStart : l.StartDate).DayNumber + 1);

        var upcoming = await ProjectRecentTasks(
                myTasks.Where(t => t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress)
                       .OrderBy(t => t.DueDate), today)
            .Take(RecentItems).ToListAsync(ct);

        var recentLeaves = await ProjectRecentLeaves(myLeaves.OrderByDescending(l => l.AppliedOn))
            .Take(RecentItems).ToListAsync(ct);

        return new EmployeeDashboardDto(tasks, leaves, approvedDays, upcoming, recentLeaves, time.UtcNow());
    }

    private static async Task<TaskStatsDto> GetTaskStatsAsync(IQueryable<TaskItem> tasks, DateOnly today, CancellationToken ct)
    {
        var byStatus = await tasks
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        var overdue = await tasks.CountAsync(t => t.DueDate < today &&
            (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress), ct);

        return new TaskStatsDto(
            byStatus.Values.Sum(),
            byStatus.GetValueOrDefault(TaskItemStatus.Pending),
            byStatus.GetValueOrDefault(TaskItemStatus.InProgress),
            byStatus.GetValueOrDefault(TaskItemStatus.Completed),
            byStatus.GetValueOrDefault(TaskItemStatus.Cancelled),
            overdue);
    }

    private static async Task<LeaveStatsDto> GetLeaveStatsAsync(IQueryable<LeaveRequest> leaves, CancellationToken ct)
    {
        var byStatus = await leaves
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        return new LeaveStatsDto(
            byStatus.GetValueOrDefault(LeaveStatus.Pending),
            byStatus.GetValueOrDefault(LeaveStatus.Approved),
            byStatus.GetValueOrDefault(LeaveStatus.Rejected));
    }

    private static IQueryable<RecentTaskDto> ProjectRecentTasks(IQueryable<TaskItem> tasks, DateOnly today) =>
        tasks.Select(t => new RecentTaskDto(
            t.Id,
            t.Title,
            t.Priority,
            t.Status,
            t.DueDate,
            t.DueDate < today && (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress),
            t.AssignedTo != null ? t.AssignedTo.FirstName + " " + t.AssignedTo.LastName : null));

    private static IQueryable<RecentLeaveDto> ProjectRecentLeaves(IQueryable<LeaveRequest> leaves) =>
        leaves.Select(l => new RecentLeaveDto(
            l.Id,
            l.Employee != null ? l.Employee.FirstName + " " + l.Employee.LastName : string.Empty,
            l.LeaveType,
            l.StartDate,
            l.EndDate,
            l.Status,
            l.AppliedOn));
}
