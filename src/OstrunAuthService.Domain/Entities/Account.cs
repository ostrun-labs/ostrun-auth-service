namespace OstrunAuthService.Domain.Entities;

// One way a user signs in: their password ("credential"), or a social
// provider identity such as ("google", <google sub>).
public sealed class Account
{
    public const string CredentialProviderId = "credential";

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string ProviderId { get; private set; } = string.Empty;
    public string ProviderAccountId { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Account()
    {
    }

    // The credential account uses the user id as its provider account id, so
    // (ProviderId, ProviderAccountId) stays unique for every kind of account.
    public static Account Credential(Guid userId, string passwordHash, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ProviderId = CredentialProviderId,
        ProviderAccountId = userId.ToString(),
        PasswordHash = passwordHash,
        CreatedAt = createdAt,
    };

    public static Account Social(Guid userId, string providerId, string providerAccountId, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ProviderId = providerId,
        ProviderAccountId = providerAccountId,
        CreatedAt = createdAt,
    };
}
