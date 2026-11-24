using System.ComponentModel.DataAnnotations;
using Starter.Domain.Users;

namespace Starter.Application.Authentication;

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(User.EmailMaxLength)] string Email,
    [Required, StringLength(128)] string Password);

public sealed record CurrentUserDto(Guid Id, string Email, string DisplayName, UserRole Role);

/// <summary>
/// The refresh token is returned to the API layer only, which places it in an HttpOnly cookie.
/// It never appears in a JSON response body.
/// </summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    CurrentUserDto User);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAtUtc);
