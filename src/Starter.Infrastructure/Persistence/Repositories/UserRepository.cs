using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Paging;
using Starter.Application.Users;
using Starter.Domain.Users;

namespace Starter.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(StarterDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Id == id && u.IsActive, cancellationToken);

    public Task<UserDto?> FindDtoAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PagedResult<UserDto>> SearchAsync(UserQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();

        if (query.NormalizedSearch() is { } search)
        {
            users = users.Where(u => u.DisplayName.Contains(search) || u.Email.Contains(search));
        }

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "email" => users.OrderByDirection(u => u.Email, query.SortDirection),
            "role" => users.OrderByDirection(u => u.Role, query.SortDirection),
            "lastlogin" => users.OrderByDirection(u => u.LastLoginAtUtc, query.SortDirection),
            _ => users.OrderByDirection(u => u.DisplayName, query.SortDirection),
        };


        return sorted.ThenBy(u => u.Id).Select(ToDto).ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<UserLookupDto>> GetActiveLookupAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserLookupDto(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);

    public void Add(User user) => db.Users.Add(user);

    private static readonly System.Linq.Expressions.Expression<Func<User, UserDto>> ToDto = u =>
        new UserDto(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.LastLoginAtUtc, u.CreatedAtUtc);
}
