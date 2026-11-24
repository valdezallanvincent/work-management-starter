using Starter.Domain.Common;
using Starter.Domain.Users;

namespace Starter.Domain.Tests;

public sealed class UserTests
{
    private static User NewUser() => User.Create("Dana.Reyes@Example.test", "Dana Reyes", UserRole.Manager, "hash");

    [Fact]
    public void Email_is_stored_in_a_normalized_form() =>
        Assert.Equal("dana.reyes@example.test", NewUser().Email);

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("@example.test")]
    [InlineData("two@@example.test")]
    [InlineData("trailing@")]
    public void Malformed_email_addresses_are_rejected(string email) =>
        Assert.Throws<DomainException>(() => User.Create(email, "Name", UserRole.Member, "hash"));

    [Fact]
    public void Account_locks_after_repeated_failures_and_unlocks_after_the_lockout_period()
    {
        var user = NewUser();
        var now = TestData.Now;

        for (var attempt = 1; attempt < User.MaxFailedLoginAttempts; attempt++)
        {
            user.RecordFailedLogin(now);
            Assert.True(user.CanSignIn(now));
        }

        user.RecordFailedLogin(now);

        Assert.False(user.CanSignIn(now));
        Assert.True(user.CanSignIn(now + User.LockoutDuration + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Successful_login_resets_the_failure_count()
    {
        var user = NewUser();
        user.RecordFailedLogin(TestData.Now);
        user.RecordFailedLogin(TestData.Now);

        user.RecordSuccessfulLogin(TestData.Now);

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal(TestData.Now, user.LastLoginAtUtc);
    }

    [Fact]
    public void Deactivated_users_cannot_sign_in()
    {
        var user = NewUser();
        user.Deactivate();

        Assert.False(user.CanSignIn(TestData.Now));
    }

    [Fact]
    public void Rotating_a_refresh_token_revokes_it_and_links_the_replacement_in_the_same_family()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash-1", Guid.NewGuid(), TestData.Now, TimeSpan.FromDays(7));

        var replacement = token.Rotate("hash-2", TestData.Now.AddMinutes(5), TimeSpan.FromDays(7));

        Assert.True(token.IsRevoked);
        Assert.Equal("rotated", token.RevocationReason);
        Assert.Equal(replacement.Id, token.ReplacedByTokenId);
        Assert.Equal(token.FamilyId, replacement.FamilyId);
        Assert.True(replacement.IsActive(TestData.Now.AddMinutes(5)));
    }

    [Fact]
    public void Expired_or_revoked_tokens_cannot_be_rotated()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Guid.NewGuid(), TestData.Now, TimeSpan.FromHours(1));

        Assert.Throws<DomainException>(() => token.Rotate("next", TestData.Now.AddHours(2), TimeSpan.FromHours(1)));

        token.Revoke(TestData.Now, "logout");
        Assert.Throws<DomainException>(() => token.Rotate("next", TestData.Now, TimeSpan.FromHours(1)));
    }
}
