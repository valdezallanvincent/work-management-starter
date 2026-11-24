using Microsoft.AspNetCore.Identity;
using Starter.Application.Common.Security;
using Starter.Domain.Users;

namespace Starter.Infrastructure.Authentication;

/// <summary>
/// Delegates to ASP.NET Core Identity's PBKDF2 hasher (versioned format, per-user salt,
/// configurable iteration count) so no cryptography is implemented here.
/// </summary>
internal sealed class IdentityPasswordHasher(IPasswordHasher<User> inner) : IPasswordHasher
{
    public string Hash(User user, string password) => inner.HashPassword(user, password);

    public PasswordVerification Verify(User user, string password) =>
        inner.VerifyHashedPassword(user, user.PasswordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed,
        };
}
