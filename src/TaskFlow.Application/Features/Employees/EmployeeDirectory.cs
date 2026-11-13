using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Caching;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Employees;

/// <summary>
/// Read-mostly employee reference data used by dropdowns and validation.
/// Lookups are cached in Redis because they are requested on every task form
/// and filter render but change only when employees are added/updated.
/// </summary>
public interface IEmployeeDirectory
{
    IQueryable<ApplicationUser> EmployeesQuery();
    Task<bool> IsActiveEmployeeAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<EmployeeLookupDto>> GetAssignableAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetDepartmentsAsync(CancellationToken ct);
    Task InvalidateAsync();
}

public sealed class EmployeeDirectory(
    IApplicationDbContext db,
    ICacheService cache,
    IOptions<CacheOptions> cacheOptions) : IEmployeeDirectory
{
    private const string EmployeeRoleNormalized = "EMPLOYEE";

    public IQueryable<ApplicationUser> EmployeesQuery() =>
        db.Users.AsNoTracking().Where(u =>
            db.UserRoles.Any(ur => ur.UserId == u.Id &&
                db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == EmployeeRoleNormalized)));

    public Task<bool> IsActiveEmployeeAsync(string userId, CancellationToken ct) =>
        EmployeesQuery().AnyAsync(u => u.Id == userId && u.IsActive, ct);

    public async Task<IReadOnlyList<EmployeeLookupDto>> GetAssignableAsync(CancellationToken ct) =>
        await cache.GetOrCreateAsync(
            CacheKeys.AssignableEmployees,
            async token => (IReadOnlyList<EmployeeLookupDto>)await EmployeesQuery()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .Select(u => new EmployeeLookupDto(u.Id, u.FirstName + " " + u.LastName, u.Department))
                .ToListAsync(token),
            cacheOptions.Value.ReferenceDataTtl,
            ct);

    public async Task<IReadOnlyList<string>> GetDepartmentsAsync(CancellationToken ct) =>
        await cache.GetOrCreateAsync(
            CacheKeys.Departments,
            async token => (IReadOnlyList<string>)await db.Users.AsNoTracking()
                .Where(u => u.Department != "")
                .Select(u => u.Department)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync(token),
            cacheOptions.Value.ReferenceDataTtl,
            ct);

    public Task InvalidateAsync() =>
        cache.RemoveAsync(CacheKeys.AssignableEmployees, CacheKeys.Departments, CacheKeys.AdminDashboard);
}
