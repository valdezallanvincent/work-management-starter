using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Persistence;
using Starter.Application.Common.Security;
using Starter.Application.Users;
using Starter.Domain.Common;
using Starter.Domain.Users;

namespace Starter.Application.Authentication;

internal sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IOptions<AuthenticationOptions> options,
    ILogger<AuthService> logger) : IAuthService
{
    // Hash of a throwaway password, computed once. Verifying against it for unknown accounts makes
    // their response time match that of real accounts, so timing does not reveal which emails exist.
    private static string? s_timingEqualizerHash;

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        string email;
        try
        {
            email = User.NormalizeEmail(request.Email);
        }
        catch (DomainException)
        {
            throw new AuthenticationFailedException();
        }

        var user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            EqualizeTiming(request.Password);
            logger.LogInformation("Login failed for unknown account");
            throw new AuthenticationFailedException();
        }

        if (!user.CanSignIn(now))
        {
            logger.LogWarning("Login rejected for user {UserId}: inactive or locked out", user.Id);
            throw new AuthenticationFailedException();
        }

        var verification = passwordHasher.Verify(user, request.Password);
        if (verification == PasswordVerification.Failed)
        {
            user.RecordFailedLogin(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Login failed for user {UserId}", user.Id);
            throw new AuthenticationFailedException();
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(user, request.Password));
        }

        user.RecordSuccessfulLogin(now);
        var session = StartSession(user, familyId: Guid.CreateVersion7(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} signed in", user.Id);
        return session;
    }

    public async Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existing = await refreshTokens.GetByHashAsync(RefreshTokenSecret.Hash(refreshToken), cancellationToken)
            ?? throw new AuthenticationFailedException();

        if (existing.IsRevoked)
        {
            // A rotated token was presented again: either the client retried with a stale cookie or the
            // token was stolen. Revoking the whole family forces a fresh login in both cases.
            await refreshTokens.RevokeFamilyAsync(existing.FamilyId, now, "reuse_detected", cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Refresh token reuse detected for user {UserId}; token family revoked", existing.UserId);
            throw new AuthenticationFailedException();
        }

        if (!existing.IsActive(now))
        {
            throw new AuthenticationFailedException();
        }

        var user = await users.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null || !user.CanSignIn(now))
        {
            existing.Revoke(now, "user_unavailable");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        var newSecret = RefreshTokenSecret.Generate();
        var replacement = existing.Rotate(RefreshTokenSecret.Hash(newSecret), now, options.Value.RefreshTokenLifetime);
        refreshTokens.Add(replacement);

        // Two concurrent refreshes with the same token: the second save fails the concurrency
        // check on the old token row and is reported as an authentication failure.
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            throw new AuthenticationFailedException();
        }

        return CreateSession(user, newSecret, replacement.ExpiresAtUtc);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var existing = await refreshTokens.GetByHashAsync(RefreshTokenSecret.Hash(refreshToken), cancellationToken);
        if (existing is null)
        {
            return;
        }

        await refreshTokens.RevokeFamilyAsync(existing.FamilyId, timeProvider.GetUtcNow(), "logout", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void EqualizeTiming(string password)
    {
        var placeholder = User.Create("timing@placeholder.invalid", "Placeholder", UserRole.Member, "pending");
        placeholder.ChangePasswordHash(s_timingEqualizerHash ??= passwordHasher.Hash(placeholder, Guid.NewGuid().ToString()));
        passwordHasher.Verify(placeholder, password);
    }

    private AuthSession StartSession(User user, Guid familyId, DateTimeOffset now)
    {
        var secret = RefreshTokenSecret.Generate();
        var token = RefreshToken.Issue(user.Id, RefreshTokenSecret.Hash(secret), familyId, now, options.Value.RefreshTokenLifetime);
        refreshTokens.Add(token);
        return CreateSession(user, secret, token.ExpiresAtUtc);
    }

    private AuthSession CreateSession(User user, string refreshSecret, DateTimeOffset refreshExpiresAt)
    {
        var accessToken = accessTokenIssuer.Issue(user);
        return new AuthSession(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshSecret,
            refreshExpiresAt,
            new CurrentUserDto(user.Id, user.Email, user.DisplayName, user.Role));
    }
}
