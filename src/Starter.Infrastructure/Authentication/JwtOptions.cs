using System.ComponentModel.DataAnnotations;

namespace Starter.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = null!;

    [Required]
    public string Audience { get; set; } = null!;

    /// <summary>
    /// HMAC-SHA256 key of at least 32 bytes. Supplied through user secrets or environment variables.
    /// A single API that both issues and validates tokens can use a symmetric key; split issuers
    /// and validators should move to an asymmetric key so validators cannot mint tokens.
    /// </summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = null!;
}
