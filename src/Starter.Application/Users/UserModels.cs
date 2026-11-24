using System.ComponentModel.DataAnnotations;
using Starter.Application.Common.Paging;
using Starter.Domain.Users;

namespace Starter.Application.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    DateTimeOffset? LastLoginAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record UserLookupDto(Guid Id, string DisplayName);

public sealed record UserQuery : PageQuery
{
    public UserRole? Role { get; init; }

    public bool? IsActive { get; init; }
}

public sealed record CreateUserRequest(
    [Required, EmailAddress, StringLength(User.EmailMaxLength)] string Email,
    [Required, StringLength(User.DisplayNameMaxLength, MinimumLength = 1)] string DisplayName,
    [EnumDataType(typeof(UserRole))] UserRole Role,
    [Required, StringLength(128, MinimumLength = PasswordPolicy.MinimumLength)] string InitialPassword);

public sealed record UpdateUserRequest(
    [EnumDataType(typeof(UserRole))] UserRole Role,
    bool IsActive);

public sealed record UpdateProfileRequest(
    [Required, StringLength(User.DisplayNameMaxLength, MinimumLength = 1)] string DisplayName);

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = PasswordPolicy.MinimumLength)] string NewPassword);
