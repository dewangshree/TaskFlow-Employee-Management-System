using Serilog;
using TaskFlow.Application.Caching;
using TaskFlow.Application.Features.Notifications;
using TaskFlow.Infrastructure;
using TaskFlow.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "TaskFlow.Worker"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddScoped<INotificationEventHandler, NotificationEventHandler>();
builder.Services.AddScoped<TaskFlow.Application.Abstractions.IApplicationDbContext>(sp =>
    sp.GetRequiredService<TaskFlow.Infrastructure.Persistence.AppDbContext>());

builder.Services.AddHostedService<OutboxPublisherService>();
builder.Services.AddHostedService<NotificationConsumerService>();

await builder.Build().RunAsync();
