using System.ComponentModel.DataAnnotations;

namespace OstrunAuthService.Infrastructure.Security;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    // HS256 rejects keys under 256 bits at signing time, which would only
    // surface as a 500 on the first successful login. 32 chars is at least
    // 32 UTF-8 bytes.
    [Required]
    [MinLength(32)]
    public required string Secret { get; init; }

    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    public int ExpirationMinutes { get; init; } = 60;
}
