using Starter.Domain.Common;

namespace Starter.Application.Users;

/// <summary>
/// Length-based policy in line with NIST SP 800-63B: long passwords are encouraged and
/// composition rules are avoided because they push users toward predictable patterns.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;

    public static void EnsureAcceptable(string password, string email)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
        {
            throw new DomainException("user.password_too_short", $"Password must be at least {MinimumLength} characters.");
        }

        if (password.Contains(email.Split('@')[0], StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("user.password_contains_email", "Password must not contain your email name.");
        }
    }
}
