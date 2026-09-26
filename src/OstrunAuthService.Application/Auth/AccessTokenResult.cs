namespace OstrunAuthService.Application.Auth;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
