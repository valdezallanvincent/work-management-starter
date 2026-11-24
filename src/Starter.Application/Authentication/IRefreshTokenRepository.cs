using Starter.Domain.Users;

namespace Starter.Application.Authentication;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, string reason, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}
