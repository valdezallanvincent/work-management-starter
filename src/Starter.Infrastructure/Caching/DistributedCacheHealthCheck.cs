using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Starter.Infrastructure.Caching;

/// <summary>
/// Reports Degraded rather than Unhealthy: the application keeps working without Redis, so an
/// outage should be visible in monitoring but must not take instances out of the load balancer.
/// </summary>
internal sealed class DistributedCacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.GetAsync("health:probe", cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Redis is unreachable; caching falls back to the database.", exception);
        }
    }
}
