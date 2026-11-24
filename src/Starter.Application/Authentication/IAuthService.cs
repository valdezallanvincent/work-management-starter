namespace Starter.Application.Authentication;

public interface IAuthService
{
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
}
