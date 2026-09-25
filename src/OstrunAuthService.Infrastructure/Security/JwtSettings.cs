using System.ComponentModel.DataAnnotations;

namespace OstrunAuthService.Infrastructure.Security;

public sealed class JwtSettings : IValidatableObject
{
    public const string SectionName = "Jwt";

    // Base64-encoded PEM RSA private key. Consumers verify tokens with the
    // public half published at /auth/.well-known/jwks.json, so they can't mint
    // tokens themselves.
    [Required]
    public required string SigningKey { get; init; }

    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    public int ExpirationMinutes { get; init; } = 60;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        string? error = null;
        try
        {
            JwtSigningKey.FromBase64Pem(SigningKey);
        }
        catch (FormatException ex)
        {
            error = $"{nameof(SigningKey)} {ex.Message}";
        }

        if (error is not null)
        {
            yield return new ValidationResult(error, [nameof(SigningKey)]);
        }
    }
}
