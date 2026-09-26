namespace OstrunAuthService.Application.Auth;

// The session token goes into a cookie, never into the response body.
public sealed record LoginOutcome(AccessTokenResult AccessToken, IssuedSession Session);

public sealed record IssuedSession(string Token, DateTime ExpiresAtUtc);
