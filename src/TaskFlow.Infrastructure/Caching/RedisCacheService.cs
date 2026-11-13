using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TaskFlow.Application.Caching;
using TaskFlow.Infrastructure.Messaging;

namespace TaskFlow.Infrastructure.Caching;

/// <summary>
/// Cache-aside over Redis: read from cache; on a miss load from PostgreSQL and
/// populate the cache with a TTL. Any Redis error is logged and the request falls
/// through to the database, so Redis is an optimisation, never a dependency.
/// </summary>
public sealed class RedisCacheService(RedisConnection redis, ILogger<RedisCacheService> logger) : ICacheService
{
    public async Task<T> GetOrCreateAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = redis.TryGetDatabase();
        if (db is null)
            return await factory(cancellationToken);

        try
        {
            var cached = await db.StringGetAsync(key);
            if (cached.HasValue)
            {
                logger.LogDebug("Cache hit {CacheKey}", key);
                return JsonSerializer.Deserialize<T>((byte[])cached!, MessagingJson.Options)!;
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or JsonException)
        {
            redis.ReportFailure(ex);
            return await factory(cancellationToken);
        }

        logger.LogDebug("Cache miss {CacheKey}", key);
        var value = await factory(cancellationToken);

        try
        {
            await db.StringSetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value, MessagingJson.Options), ttl);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            redis.ReportFailure(ex);
        }

        return value;
    }

    public async Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var redisKeys = keys.Distinct().Select(k => (RedisKey)k).ToArray();
        if (redisKeys.Length == 0)
            return;

        var db = redis.TryGetDatabase();
        if (db is null)
        {
            // Entries written before the outage still expire via their TTL, which bounds staleness.
            logger.LogWarning("Redis unavailable; could not invalidate {Count} cache keys (TTL will expire them)", redisKeys.Length);
            return;
        }

        try
        {
            await db.KeyDeleteAsync(redisKeys);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            redis.ReportFailure(ex);
        }
    }
}
