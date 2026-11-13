using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Creates roles always; demo users/data only when enabled.</summary>
    public bool DemoData { get; set; }
    public string? AdminPassword { get; set; }
    public string? EmployeePassword { get; set; }
}

/// <summary>
/// Applies migrations (optional) and seeds roles plus the same demo dataset the
/// original MVC application shipped with. Demo passwords come from configuration
/// (environment/.env), never from source code.
/// </summary>
public static class DbSeeder
{
    public static async Task InitializeAsync(IServiceProvider services, bool applyMigrations, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = sp.GetRequiredService<AppDbContext>();

        if (!db.Database.IsNpgsql())
        {
            // Migrations are PostgreSQL-specific; SQLite fallback creates the schema from the model.
            await db.Database.EnsureCreatedAsync(ct);
            // WAL lets readers proceed while a write is in progress.
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        }
        else if (applyMigrations)
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(ct);
        }

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { RoleNames.Admin, RoleNames.Employee })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var options = sp.GetRequiredService<IConfiguration>().GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new();
        if (!options.DemoData)
            return;

        if (string.IsNullOrWhiteSpace(options.AdminPassword) || string.IsNullOrWhiteSpace(options.EmployeePassword))
        {
            logger.LogWarning("Seed:DemoData is enabled but Seed:AdminPassword/Seed:EmployeePassword are not configured; skipping demo users");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await EnsureUserAsync(userManager, logger, "admin@taskflow.com", "System", "Admin", "Administration",
            DateTime.UtcNow.AddYears(-2), options.AdminPassword, RoleNames.Admin);

        var employees = new List<ApplicationUser>();
        foreach (var (email, first, last, dept) in new[]
                 {
                     ("john.doe@taskflow.com", "John", "Doe", "Engineering"),
                     ("jane.smith@taskflow.com", "Jane", "Smith", "Marketing"),
                     ("mike.johnson@taskflow.com", "Mike", "Johnson", "HR"),
                 })
        {
            var user = await EnsureUserAsync(userManager, logger, email, first, last, dept,
                DateTime.UtcNow.AddMonths(-6), options.EmployeePassword, RoleNames.Employee);
            if (user is not null) employees.Add(user);
        }

        if (admin is null || employees.Count < 3)
            return;

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        if (!await db.TaskItems.AnyAsync(ct))
        {
            var tasks = new[]
            {
                SeedTask("Setup Development Environment", "Install required tools and configure the local development environment.",
                    TaskPriority.High, TaskItemStatus.Completed, today.AddDays(-5), now.AddDays(-10), employees[0].Id, admin.Id),
                SeedTask("Design Dashboard UI", "Create wireframes and mockups for the admin and employee dashboards.",
                    TaskPriority.Medium, TaskItemStatus.InProgress, today.AddDays(3), now.AddDays(-3), employees[1].Id, admin.Id),
                SeedTask("Write API Documentation", "Document all endpoints and provide usage examples.",
                    TaskPriority.Low, TaskItemStatus.Pending, today.AddDays(14), now.AddDays(-1), employees[2].Id, admin.Id),
                SeedTask("Review Security Policies", "Audit current security policies and recommend improvements.",
                    TaskPriority.Critical, TaskItemStatus.Pending, today.AddDays(7), now, employees[0].Id, admin.Id),
            };
            db.TaskItems.AddRange(tasks);
        }

        if (!await db.LeaveRequests.AnyAsync(ct))
        {
            db.LeaveRequests.AddRange(
                LeaveRequest.Historical(employees[0].Id, LeaveType.Annual, today.AddDays(10), today.AddDays(12),
                    "Family vacation planned.", LeaveStatus.Pending, now.AddDays(-2), null, null),
                LeaveRequest.Historical(employees[1].Id, LeaveType.Sick, today.AddDays(-3), today.AddDays(-2),
                    "Medical appointment and recovery.", LeaveStatus.Approved, now.AddDays(-5), "Approved. Get well soon.", admin.Id),
                LeaveRequest.Historical(employees[2].Id, LeaveType.Casual, today.AddDays(20), today.AddDays(20),
                    "Personal errand.", LeaveStatus.Rejected, now.AddDays(-1), "Critical project deadline on that date.", admin.Id));
        }

        await db.SaveChangesAsync(ct);
    }

    private static TaskItem SeedTask(string title, string description, TaskPriority priority, TaskItemStatus status,
        DateOnly due, DateTime createdAt, string assigneeId, string adminId)
    {
        var task = TaskItem.Create(title, description, priority, due, adminId, assigneeId, createdAt);
        task.ChangeStatus(status, adminId, changedByAdmin: true, createdAt);
        // Historical seed data should not produce notifications.
        task.ClearDomainEvents();
        return task;
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager, ILogger logger,
        string email, string first, string last, string department, DateTime joinDate, string password, string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return existing;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = first,
            LastName = last,
            Department = department,
            JoinDate = joinDate,
            IsActive = true,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Could not seed {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return null;
        }

        await userManager.AddToRoleAsync(user, role);
        return user;
    }
}
