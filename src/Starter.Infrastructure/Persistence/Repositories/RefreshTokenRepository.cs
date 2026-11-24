using Microsoft.EntityFrameworkCore;
using Starter.Application.Authentication;
using Starter.Domain.Users;

namespace Starter.Infrastructure.Persistence.Repositories;

/// <summary>
/// Revocations load the affected tokens instead of using a set-based update so they are saved in
/// the same transaction as the change that caused them (for example a password change).
/// A user has only a handful of active tokens, so the extra rows read are negligible.
/// </summary>
internal sealed class RefreshTokenRepository(StarterDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        tokens.ForEach(t => t.Revoke(now, reason));
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        tokens.ForEach(t => t.Revoke(now, reason));
    }

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}
