# Caching

## What is cached, and what is not

Only two lists are cached, both behind picker controls that appear on many screens:

| Key | Content | Invalidated when |
| --- | --- | --- |
| `projects:lookup:v1` | Active projects (id, key, name) | a project is created, edited, archived or restored |
| `users:lookup:v1` | Active users (id, display name) | a user is created, updated, or changes their display name |

They are read far more often than they change, small, and identical for every user. Work item
lists, details and the dashboard are **not** cached: they change constantly, are filtered per
request, and a stale status or assignee would cause real mistakes. Their queries are indexed
instead. Caching them would add invalidation complexity for little gain.

The `:v1` suffix lets a change to the cached shape ship without colliding with old entries
still in Redis.

## The abstraction

```csharp
public interface IAppCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}
```

A read-through cache with explicit invalidation. Application services depend on this interface;
`Caching:Provider` selects the implementation at startup:

| Provider | Implementation | Backed by |
| --- | --- | --- |
| `Memory` | `MemoryAppCache` | `IMemoryCache`, objects kept as-is in process memory |
| `Redis` | `RedisAppCache` | `IDistributedCache` (StackExchange.Redis), JSON-serialised |

Expiration is configurable (`Caching:DefaultExpiration`, `Caching:LookupExpiration`). The key
prefix (`Caching:KeyPrefix`) keeps environments apart when they share a Redis instance.

.NET also ships `HybridCache`, which combines both tiers and adds stampede protection and tag
invalidation. It is a reasonable replacement; this repository uses a two-method interface to keep
failure behaviour explicit and visible in a few lines of code. See
[ADR 0005](decisions/0005-distributed-caching.md).

## When to use which

**In-memory** when there is a single instance, or when slightly stale data on other instances
is acceptable until expiry. It is the fastest option, has no network hop, no serialisation and
no extra infrastructure. Its weakness: `RemoveAsync` only clears the instance that handled the
change. With three instances, the other two keep serving the old project list until it expires.

**Redis** when running more than one instance and invalidation must be seen everywhere, or when a
cold start of every instance would hammer the database. It costs a network round trip and
serialisation, and adds a service to operate.

A two-tier design (memory in front of Redis, with pub/sub to evict local copies) gives both
speed and consistency; that is what `HybridCache` provides, and it is the natural next step if
lookup traffic grows.

## Invalidation

Invalidation is explicit and happens after the database commit:

```
UpdateAsync → SaveChangesAsync (commit) → cache.RemoveAsync(ProjectLookup)
```

Removing after commit means a concurrent reader can repopulate the cache only with committed
data. There is still a small window: a reader that loaded the old list before the commit may
write it into the cache just after the removal, leaving it stale until expiry. Short expirations
bound that risk; they are the safety net for every invalidation path.

The Angular client keeps its own copy of the lookups for the session and drops it when the user
changes projects or users from the UI.

## When Redis is unavailable

The application keeps working. `RedisAppCache` fails open:

1. A Redis read or write that throws is logged as a warning, and the value is loaded from the
   database as if it were a cache miss.
2. After a failure, Redis is bypassed for 30 seconds. Without this back-off every request would
   wait for a connection timeout before falling back.
3. Connection timeouts are short (2 seconds to connect, 1 second per operation) and
   `AbortOnConnectFail` is off, so startup does not depend on Redis either.
4. `/health/ready` reports `Degraded`, not `Unhealthy`, for Redis. Monitoring sees the problem,
   but instances are not taken out of the load balancer for a dependency they can live without.

What is lost: during the outage every lookup hits the database, and **an invalidation attempted
while Redis is down is lost**. When Redis comes back it may serve an entry that should have been
removed until that entry expires. The lookup expiration (10 minutes by default) is the bound on
that staleness. If that is not acceptable for some data, it should not be cached, or the
invalidation should be made durable (for example, published through the outbox).

These behaviours were verified by stopping the Redis container while the API was running, and
the fallback and back-off logic is covered by `CachingTests`.
