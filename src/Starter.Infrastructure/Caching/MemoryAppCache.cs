using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Caching;

namespace Starter.Infrastructure.Caching;

/// <summary>
/// Per-process cache. Fastest option and has no failure modes of its own, but each instance holds
/// its own copy, so an invalidation on one instance does not reach the others until expiry.
/// </summary>
internal sealed class MemoryAppCache(IMemoryCache cache, IOptions<CachingOptions> options) : IAppCache
{
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(key, out T? cached))
        {
            return cached!;
        }

        var value = await factory(cancellationToken);
        cache.Set(key, value, expiration ?? options.Value.DefaultExpiration);
        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }
}
