using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Starter.Application.Authentication;
using Starter.Application.Common.Exceptions;
using Starter.Application.Tests.Fakes;
using Starter.Domain.Users;

namespace Starter.Application.Tests;

public sealed class AuthServiceTests
{
    private const string Password = "correct horse battery";
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryStore _store = new();
    private readonly AuthService _service;
    private readonly User _user;

    public AuthServiceTests()
    {
        _user = User.Create("lena@example.test", "Lena", UserRole.Member, "hashed:" + Password);
        _store.Users.Add(_user);
        _service = new AuthService(
            _store, _store, new PlainTextPasswordHasher(), new FakeAccessTokenIssuer(_time), _store, _time,
            Options.Create(new AuthenticationOptions()), NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Login_returns_a_session_and_stores_only_the_refresh_token_hash()
    {
        var session = await _service.LoginAsync(new LoginRequest("LENA@example.test", Password), TestContext.Current.CancellationToken);

        Assert.Equal(_user.Id, session.User.Id);
        var stored = Assert.Single(_store.RefreshTokens);
        Assert.NotEqual(session.RefreshToken, stored.TokenHash);
        Assert.Equal(RefreshTokenSecret.Hash(session.RefreshToken), stored.TokenHash);
    }

    [Theory]
    [InlineData("lena@example.test", "wrong password!")]
    [InlineData("nobody@example.test", Password)]
    [InlineData("not-an-email", Password)]
    public async Task Failed_logins_do_not_reveal_which_part_was_wrong(string email, string password)
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _service.LoginAsync(new LoginRequest(email, password), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_even_for_the_correct_password()
    {
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < User.MaxFailedLoginAttempts; i++)
        {
            await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.LoginAsync(new LoginRequest(_user.Email, "wrong password!"), ct));
        }

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.LoginAsync(new LoginRequest(_user.Email, Password), ct));

        _time.Advance(User.LockoutDuration + TimeSpan.FromSeconds(1));
        var session = await _service.LoginAsync(new LoginRequest(_user.Email, Password), ct);
        Assert.NotNull(session.AccessToken);
    }

    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var ct = TestContext.Current.CancellationToken;
        var login = await _service.LoginAsync(new LoginRequest(_user.Email, Password), ct);

        var refreshed = await _service.RefreshAsync(login.RefreshToken, ct);

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(2, _store.RefreshTokens.Count);
        Assert.True(_store.RefreshTokens[0].IsRevoked);
        Assert.True(_store.RefreshTokens[1].IsActive(_time.GetUtcNow()));
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        var ct = TestContext.Current.CancellationToken;
        var login = await _service.LoginAsync(new LoginRequest(_user.Email, Password), ct);
        var refreshed = await _service.RefreshAsync(login.RefreshToken, ct);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.RefreshAsync(login.RefreshToken, ct));

        Assert.All(_store.RefreshTokens, t => Assert.True(t.IsRevoked));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.RefreshAsync(refreshed.RefreshToken, ct));
    }

    [Fact]
    public async Task A_concurrent_refresh_that_loses_the_race_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var login = await _service.LoginAsync(new LoginRequest(_user.Email, Password), ct);
        _store.FailNextSaveWith = ConcurrencyFailure.Create();

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.RefreshAsync(login.RefreshToken, ct));
    }

    [Fact]
    public async Task Deactivated_users_cannot_refresh()
    {
        var ct = TestContext.Current.CancellationToken;
        var login = await _service.LoginAsync(new LoginRequest(_user.Email, Password), ct);
        _user.Deactivate();

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.RefreshAsync(login.RefreshToken, ct));
    }

    [Fact]
    public async Task Expired_refresh_tokens_are_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var login = await _service.LoginAsync(new LoginRequest(_user.Email, Password), ct);
        _time.Advance(new AuthenticationOptions().RefreshTokenLifetime + TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _service.RefreshAsync(login.RefreshToken, ct));
    }
}
