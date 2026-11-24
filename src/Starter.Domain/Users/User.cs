using Starter.Domain.Common;

namespace Starter.Domain.Users;

public sealed class User : AuditableEntity
{
    public const int EmailMaxLength = 256;
    public const int DisplayNameMaxLength = 100;
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User(Guid id, string email, string displayName, UserRole role, string passwordHash)
        : base(id)
    {
        Email = email;
        DisplayName = displayName;
        Role = role;
        PasswordHash = passwordHash;
        IsActive = true;
    }

    private User()
    {
    }

    public string Email { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public string PasswordHash { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockoutEndsAtUtc { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public static User Create(string email, string displayName, UserRole role, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User(
            Guid.CreateVersion7(),
            NormalizeEmail(email),
            Guard.RequiredText(displayName, DisplayNameMaxLength, "user.display_name_invalid", "Display name"),
            role,
            passwordHash);
    }

    public static string NormalizeEmail(string email)
    {
        var normalized = Guard.RequiredText(email, EmailMaxLength, "user.email_invalid", "Email").ToLowerInvariant();
        var at = normalized.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != normalized.LastIndexOf('@') || at == normalized.Length - 1)
        {
            throw new DomainException("user.email_invalid", "Email address is not valid.");
        }

        return normalized;
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndsAtUtc is { } end && end > now;

    public bool CanSignIn(DateTimeOffset now) => IsActive && !IsLockedOut(now);

    public void RecordFailedLogin(DateTimeOffset now)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLoginAttempts)
        {
            LockoutEndsAtUtc = now.Add(LockoutDuration);
            FailedLoginCount = 0;
        }
    }

    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockoutEndsAtUtc = null;
        LastLoginAtUtc = now;
    }

    public void UpdateProfile(string displayName) =>
        DisplayName = Guard.RequiredText(displayName, DisplayNameMaxLength, "user.display_name_invalid", "Display name");

    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public void ChangeRole(UserRole role) => Role = role;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
