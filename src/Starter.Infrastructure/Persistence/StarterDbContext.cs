using Microsoft.EntityFrameworkCore;
using Starter.Domain.Notifications;
using Starter.Domain.Projects;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;
using Starter.Infrastructure.Messaging;

namespace Starter.Infrastructure.Persistence;

public sealed class StarterDbContext(DbContextOptions<StarterDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<WorkItemComment> WorkItemComments => Set<WorkItemComment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StarterDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as readable strings by default: safe to reorder and readable for anyone
        // querying the database directly. Enums whose order matters opt out (see WorkItemConfiguration).
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
