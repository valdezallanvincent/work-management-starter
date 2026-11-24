using Starter.Domain.Users;

namespace Starter.Application.Common.Security;

public interface IPasswordHasher
{
    string Hash(User user, string password);

    PasswordVerification Verify(User user, string password);
}

public enum PasswordVerification
{
    Failed,
    Success,
    SuccessRehashNeeded,
}
