using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Authentication;
using Starter.Application.Common.Caching;
using Starter.Application.Common.Exceptions;
using Starter.Application.Common.Paging;
using Starter.Application.Common.Persistence;
using Starter.Application.Common.Security;
using Starter.Domain.Common;
using Starter.Domain.Users;

namespace Starter.Application.Users;

internal sealed class UserService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IAppCache cache,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IOptions<CachingOptions> cachingOptions,
    ILogger<UserService> logger) : IUserService
{
    public Task<PagedResult<UserDto>> SearchAsync(UserQuery query, CancellationToken cancellationToken) =>
        users.SearchAsync(query, cancellationToken);

    public async Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await users.FindDtoAsync(id, cancellationToken) ?? throw new NotFoundException("User", id);

    public Task<IReadOnlyList<UserLookupDto>> GetLookupAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.UserLookup,
            users.GetActiveLookupAsync,
            cachingOptions.Value.LookupExpiration,
            cancellationToken);

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("user.email_taken", "A user with this email address already exists.");
        }

        PasswordPolicy.EnsureAcceptable(request.InitialPassword, email);

        // The hasher needs a user instance, so the user is created with a placeholder hash first.
        var user = User.Create(email, request.DisplayName, request.Role, passwordHash: "pending");
        user.ChangePasswordHash(passwordHasher.Hash(user, request.InitialPassword));

        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.UserLookup, cancellationToken);

        logger.LogInformation("User {UserId} provisioned with role {Role}", user.Id, user.Role);
        return await GetAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("User", id);

        if (user.Id == currentUser.UserId && (request.Role != UserRole.Admin || !request.IsActive))
        {
            throw new DomainException("user.cannot_demote_self", "You cannot remove your own administrator access.");
        }

        user.ChangeRole(request.Role);
        if (request.IsActive)
        {
            user.Activate();
        }
        else if (user.IsActive)
        {
            user.Deactivate();
            await refreshTokens.RevokeAllForUserAsync(user.Id, timeProvider.GetUtcNow(), "user_deactivated", cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.UserLookup, cancellationToken);
        return await GetAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserEntityAsync(cancellationToken);
        user.UpdateProfile(request.DisplayName);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.UserLookup, cancellationToken);
        return await GetAsync(user.Id, cancellationToken);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserEntityAsync(cancellationToken);

        if (passwordHasher.Verify(user, request.CurrentPassword) == PasswordVerification.Failed)
        {
            throw new DomainException("user.current_password_incorrect", "The current password is incorrect.");
        }

        PasswordPolicy.EnsureAcceptable(request.NewPassword, user.Email);
        user.ChangePasswordHash(passwordHasher.Hash(user, request.NewPassword));

        // Other sessions may belong to whoever learned the old password.
        await refreshTokens.RevokeAllForUserAsync(user.Id, timeProvider.GetUtcNow(), "password_changed", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetCurrentUserEntityAsync(CancellationToken cancellationToken) =>
        await users.GetByIdAsync(currentUser.UserId, cancellationToken)
        ?? throw new NotFoundException("User", currentUser.UserId);
}
