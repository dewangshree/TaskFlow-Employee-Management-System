using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using TaskFlow.Api.Middleware;
using TaskFlow.Api.Security;
using TaskFlow.Application.Abstractions;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Health;
using TaskFlow.Infrastructure.Observability;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Api.Configuration;

public static class ServiceRegistration
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services
            .AddControllers(options => options.Filters.Add<ValidationFilter>())
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            if (CorrelationContext.CorrelationId is { } correlationId)
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true; // api-supported-versions response header
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "TaskFlow API";
            document.Info.Version = "v1";
            document.Info.Description =
                "Employee task and leave management API. Authentication uses an HttpOnly Identity cookie " +
                "(POST /api/v1/auth/login). State-changing requests require the X-XSRF-TOKEN header " +
                "matching the XSRF-TOKEN cookie issued by GET /api/v1/auth/csrf.";
            return Task.CompletedTask;
        }));

        services.AddApiIdentity(environment);
        services.AddApiRateLimiting(configuration);

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"])
            .AddCheck<OutboxHealthCheck>("outbox", tags: ["ready"]);

        if (configuration.GetValue<bool>("ReverseProxy:Enabled"))
        {
            // Behind nginx (docker-compose) the real client IP arrives in X-Forwarded-For;
            // rate limiting partitions on it. Only trust it when the API is not publicly reachable.
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        return services;
    }

    private static void AddApiIdentity(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Same password policy as the original application.
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "taskflow.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;

            // An API must answer 401/403, never redirect to an HTML login page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        // Re-validate the security stamp every minute so deactivated users lose access quickly.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfProtection.HeaderName;
            options.Cookie.Name = "taskflow.antiforgery";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        services.AddAuthorizationBuilder()
            // Secure by default: every endpoint requires authentication unless marked [AllowAnonymous].
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(RoleNames.Admin))
            .AddPolicy(Policies.EmployeeOnly, p => p.RequireRole(RoleNames.Employee));
    }
}
