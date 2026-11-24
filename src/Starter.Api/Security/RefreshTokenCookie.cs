namespace Starter.Api.Security;

/// <summary>
/// The refresh token lives in an HttpOnly cookie that script cannot read, scoped to the auth
/// endpoints so it is not sent with every API call. SameSite=Strict means a cross-site page cannot
/// make the browser send it, which is the main CSRF defence for the refresh endpoint.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "starter_refresh";
    public const string Path = "/api/v1/auth";

    public static void Append(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            // Browsers accept Secure cookies on http://localhost, so this holds in development too.
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expiresAt,
            IsEssential = true,
        });

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions { Path = Path, Secure = true, SameSite = SameSiteMode.Strict, HttpOnly = true });

    public static string? Read(HttpRequest request) => request.Cookies[Name];
}
