using Starter.Application.Common.Paging;

namespace Starter.Application.Users;

public interface IUserService
{
    Task<PagedResult<UserDto>> SearchAsync(UserQuery query, CancellationToken cancellationToken);

    Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserLookupDto>> GetLookupAsync(CancellationToken cancellationToken);

    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken);

    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);
}
