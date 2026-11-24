using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace Starter.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class HostingTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Readiness_reports_the_database_check()
    {
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/health/ready", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"database\"", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_attempts_are_rate_limited_per_client()
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:AuthenticationPermitsPerMinute", "3"));
        using var client = limited.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var credentials = new { email = "nobody@example.test", password = "irrelevant-password" };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/v1/auth/login", credentials, Ct)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Unexpected_routes_do_not_leak_details()
    {
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/v1/does-not-exist", Ct);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }
}
