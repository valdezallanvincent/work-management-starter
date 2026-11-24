# 0005. Cache only lookup data, with a switchable memory or Redis provider

Status: accepted

## Context

Project and user pickers appear on most screens and query the same small lists repeatedly. Most
other data changes often and is filtered per request. The API should run as a single instance
locally and as several instances in Azure.

## Decision

Introduce a two-method `IAppCache` (`GetOrCreateAsync`, `RemoveAsync`) with an in-memory and a
Redis implementation, chosen by configuration. Cache only the two lookup lists, invalidate them
explicitly after writes commit, and give every entry an expiry as a safety net. The Redis
implementation fails open and backs off for 30 seconds after an error.

## When distributed caching is appropriate

- More than one instance serves traffic, and an invalidation must take effect on all of them.
- Recomputing the value is expensive enough that every instance computing it independently
  (for example after a deployment) would put noticeable load on the database.
- The data is shared by many users. Per-user data rarely benefits.

It is not appropriate when the data changes on most requests, when staleness causes incorrect
decisions (work item status), or when the query is already fast and indexed. In those cases the
added latency of a network cache and the invalidation complexity outweigh the gain.

## Alternatives considered

- **`HybridCache`** (Microsoft.Extensions.Caching.Hybrid). Local plus distributed tiers, stampede
  protection and tag-based invalidation in one API. A good upgrade path. Not used here to keep the
  failure behaviour explicit and small enough to read in full.
- **Output caching or HTTP caching.** Effective for anonymous or public responses. These lookups
  require authentication, and invalidating them from write paths is harder at the HTTP layer.
- **No caching.** A valid choice at this scale; the queries are indexed and fast. The abstraction
  is included because a starter should show where caching fits and how it degrades.

## Consequences

- With the memory provider and several instances, other instances serve stale lookups until
  expiry. The documentation calls this out, and Redis fixes it.
- Invalidations attempted during a Redis outage are lost; entries expire within
  `LookupExpiration` (10 minutes by default).
- Redis outages degrade performance, not availability, and are visible as `Degraded` health.
