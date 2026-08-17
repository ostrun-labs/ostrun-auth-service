namespace OstrunAuthService.Application.Auth;

public sealed record LoginUserResult(string Token, DateTime ExpiresAtUtc);
