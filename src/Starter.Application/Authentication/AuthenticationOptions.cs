using System.ComponentModel.DataAnnotations;

namespace Starter.Application.Authentication;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    [Range(typeof(TimeSpan), "01:00:00", "30.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
}
