using Serilog;
using TaskFlow.Application.Caching;
using TaskFlow.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "TaskFlow.Worker"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration); // outbox dispatcher + RabbitMQ consumer

await builder.Build().RunAsync();
