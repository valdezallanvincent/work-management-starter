using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Starter.Application.Authentication;
using Starter.Application.Comments;
using Starter.Application.Common.Caching;
using Starter.Application.Common.Persistence;
using Starter.Application.Common.Security;
using Starter.Application.Notifications;
using Starter.Application.Projects;
using Starter.Application.Users;
using Starter.Application.WorkItems;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;
using Starter.Infrastructure.Authentication;
using Starter.Infrastructure.Caching;
using Starter.Infrastructure.Messaging;
using Starter.Infrastructure.Messaging.ServiceBus;
using Starter.Infrastructure.Persistence;
using Starter.Infrastructure.Persistence.Interceptors;
using Starter.Infrastructure.Persistence.Repositories;
using Starter.Infrastructure.Persistence.Seeding;

namespace Starter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        AddPersistence(services, configuration);
        AddAuthentication(services);
        AddCaching(services, configuration);
        AddMessaging(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

        services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName);
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        services.AddScoped<AuditingInterceptor>();
        services.AddScoped<DomainEventOutboxInterceptor>();

        services.AddDbContext<StarterDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                sql.MigrationsHistoryTable("__EFMigrationsHistory");
            });

            // Audit values are applied before outbox rows are added, so the order matters only for readability.
            options.AddInterceptors(
                provider.GetRequiredService<AuditingInterceptor>(),
                provider.GetRequiredService<DomainEventOutboxInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IWorkItemRepository, WorkItemRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<DevelopmentDataSeeder>();

        services.AddHealthChecks().AddDbContextCheck<StarterDbContext>("database", tags: ["ready"]);
    }

    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AuthenticationOptions>().BindConfiguration(AuthenticationOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CachingOptions>().BindConfiguration(CachingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        var caching = configuration.GetSection(CachingOptions.SectionName).Get<CachingOptions>() ?? new CachingOptions();

        if (caching.Provider == CacheProvider.Redis)
        {
            var redis = configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("Caching:Provider is Redis but connection string 'Redis' is not configured.");

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse(redis);
                // Do not block startup or requests on an unreachable Redis; RedisAppCache falls back to the database.
                options.ConfigurationOptions.AbortOnConnectFail = false;
                options.ConfigurationOptions.ConnectTimeout = 2000;
                options.ConfigurationOptions.SyncTimeout = 1000;
                options.ConfigurationOptions.AsyncTimeout = 1000;
            });
            services.AddSingleton<IAppCache, RedisAppCache>();
            services.AddHealthChecks().AddCheck<DistributedCacheHealthCheck>("redis", tags: ["ready"]);
        }
        else
        {
            services.AddMemoryCache(options => options.SizeLimit = null);
            services.AddSingleton<IAppCache, MemoryAppCache>();
        }
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MessagingOptions>().BindConfiguration(MessagingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        var messaging = configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddSingleton(new MessageTypeRegistry().Register<WorkItemAssigned>("work-item-assigned.v1"));
        services.AddSingleton<MessageConsumer>();
        services.AddSingleton<OutboxProcessor>();

        if (messaging.Transport == MessageTransport.AzureServiceBus)
        {
            var connectionString = messaging.ServiceBus.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Messaging:Transport is AzureServiceBus but Messaging:ServiceBus:ConnectionString is not configured.");
            }

            // ServiceBusClient and its senders are thread-safe and expensive to create: one per application.
            services.AddSingleton(_ => new ServiceBusClient(connectionString));
            services.AddSingleton(provider => provider.GetRequiredService<ServiceBusClient>()
                .CreateSender(provider.GetRequiredService<IOptions<MessagingOptions>>().Value.ServiceBus.QueueName));
            services.AddScoped<IOutboxTransport, ServiceBusOutboxTransport>();

            if (messaging.Enabled)
            {
                services.AddHostedService<ServiceBusConsumerService>();
            }
        }
        else
        {
            services.AddScoped<IOutboxTransport, InProcessOutboxTransport>();
        }

        if (messaging.Enabled)
        {
            services.AddHostedService(provider => provider.GetRequiredService<OutboxProcessor>());
        }
    }
}
