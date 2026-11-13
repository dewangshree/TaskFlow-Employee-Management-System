using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace TaskFlow.Infrastructure.Caching;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; } = true;
    /// <summary>How long to bypass Redis after a failure before trying again.</summary>
    public int CircuitBreakSeconds { get; set; } = 30;
}

/// <summary>
/// Owns the single shared ConnectionMultiplexer and a tiny circuit breaker.
/// AbortOnConnectFail=false lets the app start (and keep serving from PostgreSQL)
/// while Redis is down; the multiplexer reconnects in the background.
/// </summary>
public sealed class RedisConnection : IDisposable
{
    private readonly Lazy<ConnectionMultiplexer?> _multiplexer;
    private readonly ILogger<RedisConnection> _logger;
    private readonly TimeSpan _circuitBreak;
    private long _openUntilTicks;

    public RedisConnection(string? connectionString, IOptions<RedisOptions> options, ILogger<RedisConnection> logger)
    {
        _logger = logger;
        _circuitBreak = TimeSpan.FromSeconds(options.Value.CircuitBreakSeconds);
        _multiplexer = new Lazy<ConnectionMultiplexer?>(() => Connect(connectionString, options.Value), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ConnectionMultiplexer? Multiplexer => _multiplexer.Value;

    /// <summary>Returns a database handle, or null when Redis should be bypassed.</summary>
    public IDatabase? TryGetDatabase()
    {
        if (DateTime.UtcNow.Ticks < Interlocked.Read(ref _openUntilTicks))
            return null;

        var mux = _multiplexer.Value;
        return mux is { IsConnected: true } ? mux.GetDatabase() : null;
    }

    public void ReportFailure(Exception ex)
    {
        Interlocked.Exchange(ref _openUntilTicks, DateTime.UtcNow.Add(_circuitBreak).Ticks);
        _logger.LogWarning(ex, "Redis operation failed; bypassing cache for {Seconds}s", _circuitBreak.TotalSeconds);
    }

    private ConnectionMultiplexer? Connect(string? connectionString, RedisOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Redis is disabled or not configured; caching is off and all reads go to PostgreSQL");
            return null;
        }

        var config = ConfigurationOptions.Parse(connectionString);
        config.AbortOnConnectFail = false;
        config.ConnectTimeout = 2000;
        config.SyncTimeout = 1000;
        config.AsyncTimeout = 1000;
        config.ClientName = "taskflow";
        return ConnectionMultiplexer.Connect(config);
    }

    public void Dispose()
    {
        if (_multiplexer.IsValueCreated)
            _multiplexer.Value?.Dispose();
    }
}
