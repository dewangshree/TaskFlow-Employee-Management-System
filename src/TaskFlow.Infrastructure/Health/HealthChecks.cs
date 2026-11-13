using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TaskFlow.Infrastructure.Caching;
using TaskFlow.Infrastructure.Messaging;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Health;

/// <summary>Redis down = Degraded (the API still works from PostgreSQL), not Unhealthy.</summary>
public sealed class RedisHealthCheck(RedisConnection redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var mux = redis.Multiplexer;
        if (mux is null)
            return HealthCheckResult.Degraded("Redis is not configured; caching disabled.");

        try
        {
            var latency = await mux.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"PING {latency.TotalMilliseconds:F1} ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Redis unreachable; serving from database.", ex);
        }
    }
}

/// <summary>
/// The API never talks to RabbitMQ directly, but a growing outbox backlog means the
/// worker or broker is down and notifications are delayed. Surface that as Degraded.
/// </summary>
public sealed class OutboxHealthCheck(AppDbContext db, IOptions<OutboxOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var pending = db.OutboxMessages.AsNoTracking().Where(m => m.ProcessedAt == null);

        var oldest = await pending.MinAsync(m => (DateTime?)m.OccurredAt, cancellationToken);
        var failed = await pending.CountAsync(m => m.Attempts >= opts.MaxAttempts, cancellationToken);
        var count = await pending.CountAsync(cancellationToken);

        var data = new Dictionary<string, object> { ["pending"] = count, ["failed"] = failed };
        if (failed > 0)
            return HealthCheckResult.Degraded($"{failed} outbox message(s) exceeded max publish attempts.", data: data);

        if (oldest is not null && DateTime.UtcNow - oldest > TimeSpan.FromSeconds(opts.BacklogWarningSeconds))
            return HealthCheckResult.Degraded($"Oldest unpublished event is {(DateTime.UtcNow - oldest.Value).TotalSeconds:F0}s old.", data: data);

        return HealthCheckResult.Healthy("Outbox draining normally.", data);
    }
}
