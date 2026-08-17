using System.ComponentModel.DataAnnotations;

namespace OstrunAuthService.Infrastructure.Security;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required]
    public required string Secret { get; init; }

    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    public int ExpirationMinutes { get; init; } = 60;
}
