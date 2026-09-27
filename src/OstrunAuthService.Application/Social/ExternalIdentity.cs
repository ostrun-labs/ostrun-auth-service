namespace OstrunAuthService.Application.Social;

// What a social provider tells us about the person who just signed in.
// ProviderAccountId is the provider's stable user id (Google "sub", GitHub
// user id), never the email, which can change.
public sealed record ExternalIdentity(
    string ProviderId,
    string ProviderAccountId,
    string? Email,
    bool EmailVerified,
    string? Name,
    string? Image);
