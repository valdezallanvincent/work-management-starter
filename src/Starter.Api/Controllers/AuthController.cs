using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Starter.Api.Configuration;
using Starter.Api.Security;
using Starter.Application.Authentication;

namespace Starter.Api.Controllers;

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, CurrentUserDto User);

[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
    /// <summary>
    /// Only login gets the strict per-IP limit: it is the password-guessing target. Refresh needs a
    /// valid cookie, and many users behind one office IP refresh routinely, so it uses the general limit.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthenticationPolicy)]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<AccessTokenResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        ToResponse(await authService.LoginAsync(request, cancellationToken));

    /// <summary>Exchanges the refresh cookie for a new access token and a rotated refresh cookie.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessTokenResponse>> Refresh(CancellationToken cancellationToken)
    {
        var token = RefreshTokenCookie.Read(Request);
        if (string.IsNullOrEmpty(token))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication failed", detail: "No refresh token was provided.");
        }

        try
        {
            return ToResponse(await authService.RefreshAsync(token, cancellationToken));
        }
        catch (Application.Common.Exceptions.AuthenticationFailedException)
        {
            RefreshTokenCookie.Delete(Response);
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(RefreshTokenCookie.Read(Request), cancellationToken);
        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    private AccessTokenResponse ToResponse(AuthSession session)
    {
        RefreshTokenCookie.Append(Response, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        return new AccessTokenResponse(session.AccessToken, session.AccessTokenExpiresAtUtc, session.User);
    }
}
