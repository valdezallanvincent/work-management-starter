namespace Starter.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Convenient for local development and containers. Production should apply migrations as a deployment step.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    public bool SeedDevelopmentData { get; set; }
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Password for every seeded account. Required when seeding; never committed.</summary>
    public string? DefaultPassword { get; set; }
}
