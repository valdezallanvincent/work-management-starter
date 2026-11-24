using Starter.Domain.Common;

namespace Starter.Domain.Users;

/// <summary>
/// A refresh token is stored only as a hash. Tokens issued from the same login share a
/// <see cref="FamilyId"/> so that reuse of a rotated token can revoke the whole chain.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken(Guid id, Guid userId, string tokenHash, Guid familyId, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? RevocationReason { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public static RefreshToken Issue(Guid userId, string tokenHash, Guid familyId, DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), userId, tokenHash, familyId, now, now.Add(lifetime));

    public bool IsActive(DateTimeOffset now) => !IsRevoked && ExpiresAtUtc > now;

    public RefreshToken Rotate(string newTokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (!IsActive(now))
        {
            throw new DomainException("auth.refresh_token_inactive", "The refresh token is no longer active.");
        }

        var replacement = Issue(UserId, newTokenHash, FamilyId, now, lifetime);
        RevokedAtUtc = now;
        RevocationReason = "rotated";
        ReplacedByTokenId = replacement.Id;
        return replacement;
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAtUtc = now;
        RevocationReason = reason;
    }
}
