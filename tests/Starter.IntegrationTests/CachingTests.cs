using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Starter.Application.Common.Caching;
using Starter.Infrastructure.Caching;

namespace Starter.IntegrationTests;

/// <summary>
/// Exercises the real cache implementations. The distributed variant runs against the in-memory
/// IDistributedCache (same contract as Redis) and against a store that fails like an unreachable Redis.
/// </summary>
public sealed class CachingTests
{
    private static readonly IOptions<CachingOptions> Options = Microsoft.Extensions.Options.Options.Create(new CachingOptions());
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Distributed_cache_serves_cached_values_until_invalidated()
    {
        var cache = new RedisAppCache(
            new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())),
            Options, new FakeTimeProvider(), NullLogger<RedisAppCache>.Instance);
        var loads = 0;
        Task<List<string>> Load(CancellationToken _) => Task.FromResult(new List<string> { $"load-{++loads}" });

        var first = await cache.GetOrCreateAsync("projects", Load, null, Ct);
        var second = await cache.GetOrCreateAsync("projects", Load, null, Ct);
        await cache.RemoveAsync("projects", Ct);
        var third = await cache.GetOrCreateAsync("projects", Load, null, Ct);

        Assert.Equal(["load-1"], first);
        Assert.Equal(["load-1"], second);
        Assert.Equal(["load-2"], third);
    }

    [Fact]
    public async Task Memory_cache_serves_cached_values_until_invalidated()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryAppCache(memory, Options);
        var loads = 0;
        Task<int> Load(CancellationToken _) => Task.FromResult(++loads);

        Assert.Equal(1, await cache.GetOrCreateAsync("key", Load, null, Ct));
        Assert.Equal(1, await cache.GetOrCreateAsync("key", Load, null, Ct));
        await cache.RemoveAsync("key", Ct);
        Assert.Equal(2, await cache.GetOrCreateAsync("key", Load, null, Ct));
    }

    [Fact]
    public async Task An_unreachable_redis_falls_back_to_the_source_and_is_skipped_during_back_off()
    {
        var store = new UnreachableDistributedCache();
        var time = new FakeTimeProvider();
        var cache = new RedisAppCache(store, Options, time, NullLogger<RedisAppCache>.Instance);

        var value = await cache.GetOrCreateAsync("projects", _ => Task.FromResult("from database"), null, Ct);
        Assert.Equal("from database", value);
        Assert.Equal(1, store.Calls);

        await cache.GetOrCreateAsync("projects", _ => Task.FromResult("from database"), null, Ct);
        Assert.Equal(1, store.Calls);

        time.Advance(TimeSpan.FromSeconds(31));
        await cache.GetOrCreateAsync("projects", _ => Task.FromResult("from database"), null, Ct);
        Assert.Equal(2, store.Calls);
    }

    private sealed class UnreachableDistributedCache : IDistributedCache
    {
        public int Calls { get; private set; }

        public byte[]? Get(string key) => Fail();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Fail());

        public void Refresh(string key) => Fail();

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.FromResult(Fail());

        public void Remove(string key) => Fail();

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.FromResult(Fail());

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Fail();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.FromResult(Fail());

        private byte[]? Fail()
        {
            Calls++;
            throw new InvalidOperationException("It was not possible to connect to the redis server(s).");
        }
    }
}
