using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Abstractions;

public sealed record JwtToken(string Value, DateTime ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    JwtToken Generate(User user);
}
