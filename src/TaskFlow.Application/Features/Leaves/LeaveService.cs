using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Caching;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Leaves;

public interface ILeaveService
{
    Task<LeaveDto> ApplyAsync(ApplyLeaveRequest request, CancellationToken ct);
    Task<PagedResult<LeaveDto>> ListMineAsync(LeaveQuery query, CancellationToken ct);
    Task<PagedResult<LeaveDto>> ListAllAsync(LeaveQuery query, CancellationToken ct);
    Task<LeaveDto> GetAsync(int id, CancellationToken ct);
    Task<LeaveDto> ReviewAsync(int id, ReviewLeaveRequest request, CancellationToken ct);
}

public sealed class LeaveService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICacheService cache,
    TimeProvider time) : ILeaveService
{
    public async Task<LeaveDto> ApplyAsync(ApplyLeaveRequest request, CancellationToken ct)
    {
        var employeeId = currentUser.RequiredUserId;

        // Prevent duplicate/overlapping requests: an employee cannot hold two
        // pending or approved requests covering the same day.
        var overlaps = await db.LeaveRequests.AnyAsync(l =>
            l.EmployeeId == employeeId &&
            (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved) &&
            l.StartDate <= request.EndDate && l.EndDate >= request.StartDate, ct);

        if (overlaps)
            throw new ConflictException("leave.overlap", "You already have a pending or approved leave request that overlaps these dates.");

        var leave = LeaveRequest.Submit(employeeId, request.LeaveType, request.StartDate, request.EndDate, request.Reason, time.UtcNow());
        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync(ct); // leave + LeaveRequested outbox event in one transaction

        await cache.RemoveAsync(CacheKeys.AdminDashboard, CacheKeys.EmployeeDashboard(employeeId));
        return await GetAsync(leave.Id, ct);
    }

    public Task<PagedResult<LeaveDto>> ListMineAsync(LeaveQuery query, CancellationToken ct)
    {
        var userId = currentUser.RequiredUserId;
        var leaves = db.LeaveRequests.AsNoTracking().Where(l => l.EmployeeId == userId);

        foreach (var pattern in SearchPattern.Terms(query.Search))
            leaves = leaves.Where(l => EF.Functions.Like(l.Reason.ToLower(), pattern, SearchPattern.EscapeCharacter));

        return ProjectPageAsync(ApplyFiltersAndSort(leaves, query), query, ct);
    }

    public Task<PagedResult<LeaveDto>> ListAllAsync(LeaveQuery query, CancellationToken ct)
    {
        var leaves = db.LeaveRequests.AsNoTracking();

        foreach (var pattern in SearchPattern.Terms(query.Search))
        {
            leaves = leaves.Where(l =>
                EF.Functions.Like(l.Reason.ToLower(), pattern, SearchPattern.EscapeCharacter) ||
                EF.Functions.Like(l.Employee!.FirstName.ToLower(), pattern, SearchPattern.EscapeCharacter) ||
                EF.Functions.Like(l.Employee!.LastName.ToLower(), pattern, SearchPattern.EscapeCharacter));
        }

        return ProjectPageAsync(ApplyFiltersAndSort(leaves, query), query, ct);
    }

    public async Task<LeaveDto> GetAsync(int id, CancellationToken ct)
    {
        var leave = await Project(db.LeaveRequests.AsNoTracking().Where(l => l.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Leave request", id);

        if (!currentUser.IsAdmin && leave.EmployeeId != currentUser.UserId)
            throw new NotFoundException("Leave request", id);

        return leave;
    }

    public async Task<LeaveDto> ReviewAsync(int id, ReviewLeaveRequest request, CancellationToken ct)
    {
        var leave = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Leave request", id);

        // Two layers of protection against double approval:
        //  1. Domain rule: only Pending requests can be reviewed (sequential case).
        //  2. Optimistic concurrency on xmin: if two admins load the same Pending
        //     request and submit simultaneously, the second commit fails with 409.
        db.SetExpectedVersion(leave, request.Version);

        var adminId = currentUser.RequiredUserId;
        if (request.Decision == LeaveDecision.Approved)
            leave.Approve(adminId, request.Comment, time.UtcNow());
        else
            leave.Reject(adminId, request.Comment, time.UtcNow());

        await db.SaveChangesAsync(ct);

        await cache.RemoveAsync(CacheKeys.AdminDashboard, CacheKeys.EmployeeDashboard(leave.EmployeeId));
        return await GetAsync(id, ct);
    }

    private static IQueryable<LeaveRequest> ApplyFiltersAndSort(IQueryable<LeaveRequest> leaves, LeaveQuery query)
    {
        if (query.Status.HasValue)
            leaves = leaves.Where(l => l.Status == query.Status);

        if (query.LeaveType.HasValue)
            leaves = leaves.Where(l => l.LeaveType == query.LeaveType);

        var dir = query.SortDirection;
        return (query.SortBy?.ToLowerInvariant()) switch
        {
            "startdate" => leaves.OrderByDirection(l => l.StartDate, dir).ThenBy(l => l.Id),
            "employee" => leaves.OrderByDirection(l => l.Employee!.FirstName, dir).ThenBy(l => l.Id),
            "status" => leaves.OrderByDirection(l => l.Status, dir).ThenBy(l => l.Id),
            _ => leaves.OrderByDirection(l => l.AppliedOn, dir).ThenBy(l => l.Id),
        };
    }

    private static Task<PagedResult<LeaveDto>> ProjectPageAsync(IQueryable<LeaveRequest> leaves, LeaveQuery query, CancellationToken ct) =>
        Project(leaves).ToPagedResultAsync(query.SafePage, query.SafePageSize, ct);

    private static IQueryable<LeaveDto> Project(IQueryable<LeaveRequest> leaves) =>
        leaves.Select(l => new LeaveDto(
            l.Id,
            l.EmployeeId,
            l.Employee != null ? l.Employee.FirstName + " " + l.Employee.LastName : string.Empty,
            l.Employee != null ? l.Employee.Department : string.Empty,
            l.LeaveType,
            l.StartDate,
            l.EndDate,
            l.EndDate.DayNumber - l.StartDate.DayNumber + 1,
            l.Reason,
            l.Status,
            l.AppliedOn,
            l.AdminComment,
            l.ReviewedBy != null ? l.ReviewedBy.FirstName + " " + l.ReviewedBy.LastName : null,
            l.ReviewedAt,
            l.Version));
}
