namespace TaskFlow.Application.Caching;

/// <summary>
/// Cache-aside abstraction. Implementations must never let a cache failure
/// break a request: on any cache error the factory (database) result is returned.
/// </summary>
public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
}

public static class CacheServiceExtensions
{
    public static Task RemoveAsync(this ICacheService cache, params string[] keys) =>
        cache.RemoveAsync(keys, CancellationToken.None);
}
