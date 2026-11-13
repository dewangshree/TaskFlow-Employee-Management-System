using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Caching;
using TaskFlow.Application.Features.Dashboard;
using TaskFlow.Application.Features.Employees;
using TaskFlow.Application.Features.Leaves;
using TaskFlow.Application.Features.Notifications;
using TaskFlow.Application.Features.Profile;
using TaskFlow.Application.Features.Tasks;

namespace TaskFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: false);
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IEmployeeDirectory, EmployeeDirectory>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationEventHandler, NotificationEventHandler>();
        return services;
    }
}
