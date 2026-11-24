using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Starter.Domain.Users;

namespace Starter.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class AuthenticationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_returns_an_access_token_and_sets_a_hardened_refresh_cookie()
    {
        var user = await factory.CreateUserAsync(UserRole.Member);
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = ApiFactory.Password }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("starter_refresh=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("accessToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wrong_password_returns_a_generic_problem_response()
    {
        var user = await factory.CreateUserAsync(UserRole.Member);
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "not-the-password" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadAsync<ProblemResponse>();
        Assert.Equal("authentication_failed", problem.Code);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_within_the_same_family()
    {
        var user = await factory.CreateUserAsync(UserRole.Member);
        using var client = await factory.SignInAsync(user);

        var refreshed = await client.PostAsync("/api/v1/auth/refresh", null, Ct);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var tokens = await factory.WithDbAsync(db => db.RefreshTokens.AsNoTracking().Where(t => t.UserId == user.Id).OrderBy(t => t.CreatedAtUtc).ToListAsync(Ct));
        Assert.Equal(2, tokens.Count);
        Assert.Equal("rotated", tokens[0].RevocationReason);
        Assert.Equal(tokens[1].Id, tokens[0].ReplacedByTokenId);
        Assert.Equal(tokens[0].FamilyId, tokens[1].FamilyId);
        Assert.False(tokens[1].IsRevoked);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_every_token_in_the_family()
    {
        var user = await factory.CreateUserAsync(UserRole.Member);
        using var client = factory.CreateHttpsClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = ApiFactory.Password }, Ct);
        var firstCookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/auth/refresh", null, Ct)).StatusCode);

        using var replay = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", firstCookie);
        var replayed = await replay.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
        var tokens = await factory.WithDbAsync(db => db.RefreshTokens.AsNoTracking().Where(t => t.UserId == user.Id).ToListAsync(Ct));
        Assert.All(tokens, t => Assert.True(t.IsRevoked));
        Assert.Contains(tokens, t => t.RevocationReason == "reuse_detected");

        // The legitimate client's current cookie is now revoked as well.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/auth/refresh", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var user = await factory.CreateUserAsync(UserRole.Member);
        using var client = await factory.SignInAsync(user);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null, Ct)).StatusCode);

        var tokens = await factory.WithDbAsync(db => db.RefreshTokens.AsNoTracking().Where(t => t.UserId == user.Id).ToListAsync(Ct));
        Assert.All(tokens, t => Assert.Equal("logout", t.RevocationReason));
    }

    [Fact]
    public async Task Protected_endpoints_require_a_valid_token()
    {
        using var client = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/projects", Ct)).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-real-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/projects", Ct)).StatusCode);
    }

    [Fact]
    public async Task User_administration_is_limited_to_administrators()
    {
        using var member = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Member));
        using var admin = await factory.SignInAsync(await factory.CreateUserAsync(UserRole.Admin));
        var newUser = new { email = $"new-{Guid.NewGuid():N}@example.test", displayName = "New Person", role = "Member", initialPassword = "a long enough passphrase" };

        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync("/api/v1/users", newUser, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/users", newUser, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/users", newUser, Ct)).StatusCode);
    }
}
