namespace OstrunAuthService.Application.Auth;

// Recorded on the session so a user can later recognize their devices.
public sealed record ClientInfo(string? IpAddress, string? UserAgent);
