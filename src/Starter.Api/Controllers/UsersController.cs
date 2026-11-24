using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Api.Security;
using Starter.Application.Common.Paging;
using Starter.Application.Common.Security;
using Starter.Application.Users;

namespace Starter.Api.Controllers;

[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController(IUserService userService, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Administrators)]
    public Task<PagedResult<UserDto>> Search([FromQuery] UserQuery query, CancellationToken cancellationToken) =>
        userService.SearchAsync(query, cancellationToken);

    /// <summary>Active users for assignment and owner pickers. Cached; see docs/caching.md.</summary>
    [HttpGet("lookup")]
    public Task<IReadOnlyList<UserLookupDto>> Lookup(CancellationToken cancellationToken) =>
        userService.GetLookupAsync(cancellationToken);

    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken cancellationToken) =>
        userService.GetAsync(currentUser.UserId, cancellationToken);

    [HttpPut("me")]
    public Task<UserDto> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        userService.UpdateProfileAsync(request, cancellationToken);

    [HttpPut("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await userService.ChangePasswordAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administrators)]
    public Task<UserDto> Get(Guid id, CancellationToken cancellationToken) =>
        userService.GetAsync(id, cancellationToken);

    /// <summary>Users are provisioned by administrators; there is no public sign-up.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Administrators)]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id, version = "1" }, user);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administrators)]
    public Task<UserDto> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        userService.UpdateAsync(id, request, cancellationToken);
}
