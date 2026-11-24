using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Starter.Application.Common.Security;
using Starter.Domain.Users;

namespace Starter.Api.Security;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserIdOrDefault =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public Guid UserId => UserIdOrDefault ?? throw new InvalidOperationException("There is no authenticated user.");

    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(AuthenticationSetup.RoleClaimType), out var role) ? role : UserRole.Member;
}
