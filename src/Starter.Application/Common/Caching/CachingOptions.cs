using System.ComponentModel.DataAnnotations;

namespace Starter.Application.Common.Caching;

public enum CacheProvider
{
    Memory,
    Redis,
}

public sealed class CachingOptions
{
    public const string SectionName = "Caching";

    public CacheProvider Provider { get; set; } = CacheProvider.Memory;

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan LookupExpiration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Prefix that keeps keys from different apps or environments apart in a shared Redis.</summary>
    [Required]
    public string KeyPrefix { get; set; } = "starter:";
}
