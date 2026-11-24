using Starter.Application.Common.Paging;
using Starter.Domain.Users;

namespace Starter.Application.Users;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken);

    Task<UserDto?> FindDtoAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<UserDto>> SearchAsync(UserQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserLookupDto>> GetActiveLookupAsync(CancellationToken cancellationToken);

    void Add(User user);
}
