using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TaskFlow.Api.Configuration;

public static class RateLimitPolicies
{
    /// <summary>Login, registration and password change: per client IP, strict.</summary>
    public const string Auth = "auth";
    /// <summary>Search/list endpoints driven by type-ahead: per user, sliding window.</summary>
    public const string Search = "search";
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public LimitOptions Global { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };
    public LimitOptions Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public LimitOptions Search { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    public sealed class LimitOptions
    {
        public int PermitLimit { get; set; }
        public int WindowSeconds { get; set; }
    }
}

public static class RateLimitingSetup
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global safety net for every endpoint: token bucket per user (or per IP when anonymous).
            // Allows short bursts while capping sustained throughput.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/health"))
                    return RateLimitPartition.GetNoLimiter("health");

                return RateLimitPartition.GetTokenBucketLimiter(PartitionKey(context), _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = options.Global.PermitLimit,
                    TokensPerPeriod = options.Global.PermitLimit,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(options.Global.WindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });

            // Brute-force protection: fixed window per IP. Complements Identity's per-account lockout,
            // which alone cannot stop one client spraying passwords across many accounts.
            limiter.AddPolicy(RateLimitPolicies.Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter($"ip:{ClientIp(context)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.Auth.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.Auth.WindowSeconds),
                    QueueLimit = 0,
                }));

            // Sliding window smooths the burst-at-window-boundary problem of fixed windows
            // for endpoints hit by debounced type-ahead search.
            limiter.AddPolicy(RateLimitPolicies.Search, context =>
                RateLimitPartition.GetSlidingWindowLimiter(PartitionKey(context), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = options.Search.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.Search.WindowSeconds),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                }));

            limiter.OnRejected = async (context, ct) =>
            {
                var http = context.HttpContext;
                int? retryAfterSeconds = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                    http.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
                }

                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiting")
                    .LogWarning("Rate limit exceeded for {Partition} on {Path}", PartitionKey(http), http.Request.Path);

                await Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too many requests",
                        detail: retryAfterSeconds is { } s
                            ? $"Request limit exceeded. Try again in {s} second(s)."
                            : "Request limit exceeded. Try again shortly.",
                        extensions: new Dictionary<string, object?> { ["code"] = "rate_limited", ["retryAfterSeconds"] = retryAfterSeconds })
                    .ExecuteAsync(http);
            };
        });

        return services;
    }

    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId ? $"user:{userId}" : $"ip:{ClientIp(context)}";

    private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
