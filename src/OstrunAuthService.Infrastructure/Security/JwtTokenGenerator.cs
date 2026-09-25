using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Security;

public sealed class JwtTokenGenerator(IOptions<JwtSettings> options, JwtSigningKey signingKey) : IJwtTokenGenerator
{
    private readonly JwtSettings _settings = options.Value;

    public JwtToken Generate(User user)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(signingKey.PrivateKey, JwtSigningKey.Algorithm));

        return new JwtToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
