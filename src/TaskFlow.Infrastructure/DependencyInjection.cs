using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Caching;
using TaskFlow.Infrastructure.Caching;
using TaskFlow.Infrastructure.Messaging;
using TaskFlow.Infrastructure.Messaging.Hosting;
using TaskFlow.Application.Features.Notifications;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
    /// <summary>PostgreSQL + Redis. Shared by the API and the worker.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL is the primary database. When no Postgres connection string is configured
        // (free single-instance hosting, quick local demo) the app falls back to an SQLite file.
        var postgres = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(postgres))
        {
            services.AddDbContext<AppDbContext>(options => options
                .UseNpgsql(postgres, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.CommandTimeout(30);
                }));
        }
        else
        {
            var sqlite = configuration.GetConnectionString("Sqlite") ?? $"Data Source={DefaultSqlitePath()}";
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(sqlite));
        }
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton(sp => new RedisConnection(
            configuration.GetConnectionString("Redis"),
            sp.GetRequiredService<IOptions<RedisOptions>>(),
            sp.GetRequiredService<ILogger<RedisConnection>>()));
        services.AddSingleton<ICacheService, RedisCacheService>();

        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        return services;
    }

    /// <summary>On Azure App Service, %HOME%\\data survives redeployments; elsewhere use the working directory.</summary>
    private static string DefaultSqlitePath()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) && !string.IsNullOrEmpty(home))
        {
            var dir = Path.Combine(home, "data", "taskflow");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "taskflow-v2.db");
        }
        return "taskflow-v2.db";
    }

    /// <summary>
    /// Background event processing (outbox dispatcher + notification consumer). Uses RabbitMQ when
    /// ConnectionStrings:RabbitMq is set; otherwise processes outbox events in-process.
    /// Hosted either by TaskFlow.Worker or inside the API (Worker:RunInApi) for single-instance deployments.
    /// </summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddScoped<OutboxDispatcher>();
        services.AddScoped<NotificationMessageProcessor>();
        services.AddScoped<INotificationEventHandler, NotificationEventHandler>();
        services.AddHostedService<OutboxPublisherService>();

        var rabbit = configuration.GetConnectionString("RabbitMq");
        if (string.IsNullOrWhiteSpace(rabbit))
        {
            services.AddSingleton<IOutboxPublisher, InProcessOutboxPublisher>();
            return services;
        }

        services.AddSingleton(sp => new RabbitMqConnection(rabbit, sp.GetRequiredService<ILogger<RabbitMqConnection>>()));
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<IOutboxPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
        services.AddHostedService<NotificationConsumerService>();
        return services;
    }
}
