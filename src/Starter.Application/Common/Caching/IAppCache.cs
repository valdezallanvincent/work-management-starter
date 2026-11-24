namespace Starter.Application.Common.Caching;

/// <summary>
/// Read-through cache for small, read-heavy reference data. Implementations must fail open:
/// if the cache store is unavailable the factory result is returned and the error is logged.
/// </summary>
public interface IAppCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
