using Starter.Domain.Users;

namespace Starter.Application.Common.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Throws when there is no authenticated user; use only on authenticated paths.</summary>
    Guid UserId { get; }

    UserRole Role { get; }

    Guid? UserIdOrDefault { get; }
}
