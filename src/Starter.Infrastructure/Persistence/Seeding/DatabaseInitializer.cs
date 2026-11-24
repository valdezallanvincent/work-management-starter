using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Starter.Infrastructure.Persistence.Seeding;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (options.ApplyMigrationsOnStartup)
        {
            var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(cancellationToken);
        }

        if (options.SeedDevelopmentData)
        {
            if (!environment.IsDevelopment())
            {
                logger.LogWarning("Development data seeding is enabled but the environment is {Environment}; skipping", environment.EnvironmentName);
                return;
            }

            await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(cancellationToken);
        }
    }
}
