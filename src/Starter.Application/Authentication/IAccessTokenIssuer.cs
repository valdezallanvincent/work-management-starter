using Starter.Domain.Users;

namespace Starter.Application.Authentication;

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
