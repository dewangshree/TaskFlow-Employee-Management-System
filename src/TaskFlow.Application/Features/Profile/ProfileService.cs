using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Caching;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Employees;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Profile;

public interface IProfileService
{
    Task<ProfileDto> GetAsync(CancellationToken ct);
    Task<ProfileDto> UpdateAsync(UpdateProfileRequest request, CancellationToken ct);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
}

public sealed class ProfileService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    UserManager<ApplicationUser> userManager,
    IEmployeeDirectory directory,
    ICacheService cache) : IProfileService
{
    public async Task<ProfileDto> GetAsync(CancellationToken ct)
    {
        var user = await GetUserAsync();
        var roles = await userManager.GetRolesAsync(user);

        var taskCounts = await db.TaskItems.AsNoTracking()
            .Where(t => t.AssignedToId == user.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(t => t.Status == TaskItemStatus.Completed),
            })
            .FirstOrDefaultAsync(ct);

        var pendingLeaves = await db.LeaveRequests.AsNoTracking()
            .CountAsync(l => l.EmployeeId == user.Id && l.Status == LeaveStatus.Pending, ct);

        return new ProfileDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.Department,
            user.JoinDate,
            roles.FirstOrDefault() ?? RoleNames.Employee,
            taskCounts?.Total ?? 0,
            taskCounts?.Completed ?? 0,
            pendingLeaves);
    }

    public async Task<ProfileDto> UpdateAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await GetUserAsync();

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Department = request.Department.Trim();

        var email = request.Email.Trim();
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            // Email is also the username (existing behaviour); both must stay in sync.
            IdentityResultGuard.ThrowIfFailed(await userManager.SetEmailAsync(user, email), "email");
            IdentityResultGuard.ThrowIfFailed(await userManager.SetUserNameAsync(user, email), "email");
        }

        IdentityResultGuard.ThrowIfFailed(await userManager.UpdateAsync(user));

        await directory.InvalidateAsync();
        await cache.RemoveAsync(CacheKeys.EmployeeDashboard(user.Id));
        return await GetAsync(ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await GetUserAsync();
        // Requires the current password (the legacy MVC flow reset it without one).
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded && result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            throw new RequestValidationException("currentPassword", "Current password is incorrect.");

        IdentityResultGuard.ThrowIfFailed(result, "newPassword");
    }

    private async Task<ApplicationUser> GetUserAsync() =>
        await userManager.FindByIdAsync(currentUser.RequiredUserId)
        ?? throw new NotFoundException("User", currentUser.RequiredUserId);
}
