namespace OstrunAuthService.Application.Sessions;

public sealed record CurrentSessionResult(SessionUser User, SessionDetails Session);

public sealed record SessionUser(Guid Id, string Email, bool EmailVerified, string? Name, string? Image);

public sealed record SessionDetails(Guid Id, DateTime ExpiresAt);
