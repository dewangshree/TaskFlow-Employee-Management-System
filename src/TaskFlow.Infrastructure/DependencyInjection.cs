using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Caching;
using TaskFlow.Infrastructure.Caching;
using TaskFlow.Infrastructure.Messaging;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
    /// <summary>PostgreSQL + Redis. Shared by the API and the worker.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgres = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(postgres, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsql.CommandTimeout(30);
            }));
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

    /// <summary>RabbitMQ publishing/consuming. Only the worker needs this.</summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var rabbit = configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException("ConnectionStrings:RabbitMq is not configured.");

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton(sp => new RabbitMqConnection(rabbit, sp.GetRequiredService<ILogger<RabbitMqConnection>>()));
        services.AddSingleton<RabbitMqPublisher>();
        services.AddScoped<OutboxDispatcher>();
        services.AddScoped<NotificationMessageProcessor>();
        return services;
    }
}
