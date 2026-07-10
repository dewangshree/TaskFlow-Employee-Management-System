using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Helpers;
using TaskFlow.Models;

namespace TaskFlow.Services;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
            await roleManager.CreateAsync(new IdentityRole(RoleNames.Admin));

        if (!await roleManager.RoleExistsAsync(RoleNames.Employee))
            await roleManager.CreateAsync(new IdentityRole(RoleNames.Employee));

        ApplicationUser? admin = null;
        if (await userManager.FindByEmailAsync("admin@taskflow.com") == null)
        {
            admin = new ApplicationUser
            {
                UserName = "admin@taskflow.com",
                Email = "admin@taskflow.com",
                EmailConfirmed = true,
                FirstName = "System",
                LastName = "Admin",
                Department = "Administration",
                JoinDate = DateTime.UtcNow.AddYears(-2),
                IsActive = true
            };

            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, RoleNames.Admin);
        }
        else
        {
            admin = await userManager.FindByEmailAsync("admin@taskflow.com");
        }

        var employees = new List<(string Email, string First, string Last, string Dept)>
        {
            ("john.doe@taskflow.com", "John", "Doe", "Engineering"),
            ("jane.smith@taskflow.com", "Jane", "Smith", "Marketing"),
            ("mike.johnson@taskflow.com", "Mike", "Johnson", "HR")
        };

        var employeeUsers = new List<ApplicationUser>();
        foreach (var (email, first, last, dept) in employees)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = first,
                    LastName = last,
                    Department = dept,
                    JoinDate = DateTime.UtcNow.AddMonths(-6),
                    IsActive = true
                };

                var result = await userManager.CreateAsync(user, "Employee@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, RoleNames.Employee);
                    employeeUsers.Add(user);
                }
            }
            else
            {
                var existing = await userManager.FindByEmailAsync(email);
                if (existing != null)
                    employeeUsers.Add(existing);
            }
        }

        if (!await context.TaskItems.AnyAsync() && admin != null && employeeUsers.Count > 0)
        {
            var tasks = new List<TaskItem>
            {
                new()
                {
                    Title = "Setup Development Environment",
                    Description = "Install required tools and configure the local development environment.",
                    Priority = TaskPriority.High,
                    Status = Models.TaskStatus.Completed,
                    DueDate = DateTime.UtcNow.Date.AddDays(-5),
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    AssignedToId = employeeUsers[0].Id,
                    CreatedById = admin.Id
                },
                new()
                {
                    Title = "Design Dashboard UI",
                    Description = "Create wireframes and mockups for the admin and employee dashboards.",
                    Priority = TaskPriority.Medium,
                    Status = Models.TaskStatus.InProgress,
                    DueDate = DateTime.UtcNow.Date.AddDays(3),
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    AssignedToId = employeeUsers[1].Id,
                    CreatedById = admin.Id
                },
                new()
                {
                    Title = "Write API Documentation",
                    Description = "Document all endpoints and provide usage examples.",
                    Priority = TaskPriority.Low,
                    Status = Models.TaskStatus.Pending,
                    DueDate = DateTime.UtcNow.Date.AddDays(14),
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    AssignedToId = employeeUsers[2].Id,
                    CreatedById = admin.Id
                },
                new()
                {
                    Title = "Review Security Policies",
                    Description = "Audit current security policies and recommend improvements.",
                    Priority = TaskPriority.Critical,
                    Status = Models.TaskStatus.Pending,
                    DueDate = DateTime.UtcNow.Date.AddDays(7),
                    CreatedAt = DateTime.UtcNow,
                    AssignedToId = employeeUsers[0].Id,
                    CreatedById = admin.Id
                }
            };

            context.TaskItems.AddRange(tasks);
        }

        if (!await context.LeaveRequests.AnyAsync() && employeeUsers.Count > 0)
        {
            var leaves = new List<LeaveRequest>
            {
                new()
                {
                    EmployeeId = employeeUsers[0].Id,
                    LeaveType = LeaveType.Annual,
                    StartDate = DateTime.UtcNow.Date.AddDays(10),
                    EndDate = DateTime.UtcNow.Date.AddDays(12),
                    Reason = "Family vacation planned.",
                    Status = LeaveStatus.Pending,
                    AppliedOn = DateTime.UtcNow.AddDays(-2)
                },
                new()
                {
                    EmployeeId = employeeUsers[1].Id,
                    LeaveType = LeaveType.Sick,
                    StartDate = DateTime.UtcNow.Date.AddDays(-3),
                    EndDate = DateTime.UtcNow.Date.AddDays(-2),
                    Reason = "Medical appointment and recovery.",
                    Status = LeaveStatus.Approved,
                    AppliedOn = DateTime.UtcNow.AddDays(-5),
                    AdminComment = "Approved. Get well soon."
                },
                new()
                {
                    EmployeeId = employeeUsers[2].Id,
                    LeaveType = LeaveType.Casual,
                    StartDate = DateTime.UtcNow.Date.AddDays(20),
                    EndDate = DateTime.UtcNow.Date.AddDays(20),
                    Reason = "Personal errand.",
                    Status = LeaveStatus.Rejected,
                    AppliedOn = DateTime.UtcNow.AddDays(-1),
                    AdminComment = "Critical project deadline on that date."
                }
            };

            context.LeaveRequests.AddRange(leaves);
        }

        await context.SaveChangesAsync();
    }
}
