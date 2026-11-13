using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Employees;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> ListAsync(EmployeeQuery query, CancellationToken ct);
    Task<EmployeeDto> SetActiveAsync(string id, SetEmployeeActiveRequest request, CancellationToken ct);
}

public sealed class EmployeeService(
    IApplicationDbContext db,
    IEmployeeDirectory directory,
    UserManager<ApplicationUser> userManager) : IEmployeeService
{
    public Task<PagedResult<EmployeeDto>> ListAsync(EmployeeQuery query, CancellationToken ct)
    {
        // Previously every employee was loaded into memory and filtered in C#.
        // Now filtering, sorting and paging all execute in PostgreSQL.
        var employees = directory.EmployeesQuery();

        if (!string.IsNullOrWhiteSpace(query.Department))
            employees = employees.Where(u => u.Department == query.Department);

        if (query.IsActive.HasValue)
            employees = employees.Where(u => u.IsActive == query.IsActive.Value);

        foreach (var pattern in SearchPattern.Terms(query.Search))
        {
            employees = employees.Where(u =>
                EF.Functions.Like(u.FirstName.ToLower(), pattern, SearchPattern.EscapeCharacter) ||
                EF.Functions.Like(u.LastName.ToLower(), pattern, SearchPattern.EscapeCharacter) ||
                EF.Functions.Like(u.Email!.ToLower(), pattern, SearchPattern.EscapeCharacter) ||
                EF.Functions.Like(u.Department.ToLower(), pattern, SearchPattern.EscapeCharacter));
        }

        // Task counts are correlated sub-queries in the same statement: one round trip, no N+1.
        var projected = employees.Select(u => new
        {
            User = u,
            TotalTasks = db.TaskItems.Count(t => t.AssignedToId == u.Id),
            OpenTasks = db.TaskItems.Count(t => t.AssignedToId == u.Id &&
                (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress)),
        });

        var dir = query.SortDirection;
        projected = (query.SortBy?.ToLowerInvariant()) switch
        {
            "email" => projected.OrderByDirection(x => x.User.Email, dir).ThenBy(x => x.User.Id),
            "department" => projected.OrderByDirection(x => x.User.Department, dir).ThenBy(x => x.User.FirstName),
            "joindate" => projected.OrderByDirection(x => x.User.JoinDate, dir).ThenBy(x => x.User.Id),
            "opentasks" => projected.OrderByDirection(x => x.OpenTasks, dir).ThenBy(x => x.User.FirstName),
            _ => projected.OrderByDirection(x => x.User.FirstName, dir).ThenByDirection(x => x.User.LastName, dir),
        };

        return projected
            .Select(x => new EmployeeDto(
                x.User.Id,
                x.User.FirstName,
                x.User.LastName,
                x.User.FirstName + " " + x.User.LastName,
                x.User.Email ?? string.Empty,
                x.User.Department,
                x.User.JoinDate,
                x.User.IsActive,
                x.TotalTasks,
                x.OpenTasks))
            .ToPagedResultAsync(query.SafePage, query.SafePageSize, ct);
    }

    public async Task<EmployeeDto> SetActiveAsync(string id, SetEmployeeActiveRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id) ?? throw new NotFoundException("Employee", id);

        if (!await userManager.IsInRoleAsync(user, RoleNames.Employee))
            throw new ConflictException("employee.not_employee", "Only employee accounts can be activated or deactivated.");

        if (user.IsActive != request.IsActive)
        {
            user.IsActive = request.IsActive;

            // Rotating the security stamp invalidates the deactivated user's existing
            // auth cookies at the next validation interval, not only at their next login.
            var result = request.IsActive
                ? await userManager.UpdateAsync(user)
                : await userManager.UpdateSecurityStampAsync(user);
            IdentityResultGuard.ThrowIfFailed(result);

            await directory.InvalidateAsync();
        }

        return await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new EmployeeDto(
                u.Id, u.FirstName, u.LastName, u.FirstName + " " + u.LastName, u.Email ?? string.Empty,
                u.Department, u.JoinDate, u.IsActive,
                db.TaskItems.Count(t => t.AssignedToId == u.Id),
                db.TaskItems.Count(t => t.AssignedToId == u.Id &&
                    (t.Status == TaskItemStatus.Pending || t.Status == TaskItemStatus.InProgress))))
            .SingleAsync(ct);
    }
}

internal static class OrderingExtensions
{
    public static IOrderedQueryable<T> ThenByDirection<T, TKey>(
        this IOrderedQueryable<T> source, System.Linq.Expressions.Expression<Func<T, TKey>> key, SortDirection dir) =>
        dir == SortDirection.Asc ? source.ThenBy(key) : source.ThenByDescending(key);
}

public static class IdentityResultGuard
{
    public static void ThrowIfFailed(IdentityResult result, string field = "")
    {
        if (result.Succeeded) return;

        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
            throw new ConflictException("concurrency", "This record was modified by someone else. Reload and try again.");

        throw new RequestValidationException(new Dictionary<string, string[]>
        {
            [field] = result.Errors.Select(e => e.Description).ToArray(),
        });
    }
}
