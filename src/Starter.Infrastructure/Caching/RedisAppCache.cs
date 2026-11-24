using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Caching;

namespace Starter.Infrastructure.Caching;

/// <summary>
/// Shared cache backed by Redis through <see cref="IDistributedCache"/>. Fails open: when Redis
/// cannot be reached the value is loaded from the database, and Redis is skipped for a short
/// back-off period so requests are not each delayed by a connection timeout.
/// </summary>
internal sealed class RedisAppCache(
    IDistributedCache cache,
    IOptions<CachingOptions> options,
    TimeProvider timeProvider,
    ILogger<RedisAppCache> logger) : IAppCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan BackOff = TimeSpan.FromSeconds(30);

    // Ticks of the time until which Redis is bypassed. Shared across requests because the class is a singleton.
    private long _bypassUntilTicks;

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration, CancellationToken cancellationToken)
    {
        var fullKey = options.Value.KeyPrefix + key;

        if (IsAvailable())
        {
            try
            {
                var bytes = await cache.GetAsync(fullKey, cancellationToken);
                if (bytes is not null)
                {
                    return JsonSerializer.Deserialize<T>(bytes, JsonOptions)!;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                MarkUnavailable(exception, "read");
            }
        }

        var value = await factory(cancellationToken);

        if (IsAvailable())
        {
            try
            {
                await cache.SetAsync(
                    fullKey,
                    JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration ?? options.Value.DefaultExpiration },
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                MarkUnavailable(exception, "write");
            }
        }

        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        // Invalidation is attempted even during back-off: a missed removal leaves stale data until expiry.
        try
        {
            await cache.RemoveAsync(options.Value.KeyPrefix + key, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkUnavailable(exception, "invalidate");
            logger.LogWarning("Cache key {CacheKey} could not be invalidated; it will expire naturally", key);
        }
    }

    private bool IsAvailable() => timeProvider.GetUtcNow().UtcTicks >= Interlocked.Read(ref _bypassUntilTicks);

    private void MarkUnavailable(Exception exception, string operation)
    {
        Interlocked.Exchange(ref _bypassUntilTicks, timeProvider.GetUtcNow().Add(BackOff).UtcTicks);
        logger.LogWarning(exception, "Redis cache {Operation} failed; using the database for the next {BackOffSeconds}s", operation, BackOff.TotalSeconds);
    }
}
