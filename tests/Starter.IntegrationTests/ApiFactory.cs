using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Starter.Application.Common.Security;
using Starter.Domain.Users;
using Starter.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Starter.IntegrationTests;

/// <summary>
/// One SQL Server container per test run, shared by every test class in the collection. Tests create
/// their own users and projects with unique keys, so they do not depend on each other or on order.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "integration-test-password";

    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        // Creating the client builds the host, which applies migrations (Database:ApplyMigrationsOnStartup).
        CreateClient().Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString().Replace("Database=master", "Database=StarterTests", StringComparison.Ordinal));
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-with-enough-length");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Caching:Provider", "Memory");
        // The outbox dispatcher is driven explicitly by tests so assertions are deterministic.
        builder.UseSetting("Messaging:Enabled", "false");
        builder.UseSetting("RateLimiting:AuthenticationPermitsPerMinute", "1000");
        builder.UseSetting("RateLimiting:ApiPermitsPerMinute", "10000");
    }

    /// <summary>Cookies are marked Secure, so the client uses an https base address (TestServer ignores the scheme).</summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    public async Task<User> CreateUserAsync(UserRole role, string? email = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StarterDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = User.Create(email ?? $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@example.test", $"{role} {Random.Shared.Next(1000)}", role, "pending");
        user.ChangePasswordHash(hasher.Hash(user, Password));
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<HttpClient> SignInAsync(User user)
    {
        var client = CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<StarterDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<StarterDbContext>());
    }

    public static string UniqueKey() => "T" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();

    public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
