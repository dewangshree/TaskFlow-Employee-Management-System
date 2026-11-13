using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using TaskFlow.Api.Configuration;
using TaskFlow.Api.Middleware;
using TaskFlow.Api.Security;
using TaskFlow.Application;
using TaskFlow.Infrastructure;
using TaskFlow.Infrastructure.Observability;
using TaskFlow.Infrastructure.Persistence;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "TaskFlow.Api"));

    builder.Services
        .AddApplication(builder.Configuration)
        .AddInfrastructure(builder.Configuration)
        .AddApiServices(builder.Configuration, builder.Environment);

    var app = builder.Build();

    await DbSeeder.InitializeAsync(
        app.Services,
        applyMigrations: app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"));

    if (app.Configuration.GetValue<bool>("ReverseProxy:Enabled"))
        app.UseForwardedHeaders();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnostics, http) =>
        {
            diagnostics.Set("UserId", http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            diagnostics.Set(CorrelationContext.LogPropertyName, CorrelationContext.CorrelationId);
        };
    });
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    if (app.Configuration.GetValue("OpenApi:Enabled", app.Environment.IsDevelopment()))
    {
        app.MapOpenApi().AllowAnonymous();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "TaskFlow API v1");
            options.RoutePrefix = "swagger";
        });
    }

    app.UseAuthentication();
    app.UseRateLimiter(); // after authentication so limits can partition by user id
    app.UseMiddleware<CsrfValidationMiddleware>();
    app.UseAuthorization();

    app.MapControllers();

    // Liveness: process is up. Readiness: dependencies reachable (Redis/outbox issues report Degraded, still 200).
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync,
    }).AllowAnonymous();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "TaskFlow API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
