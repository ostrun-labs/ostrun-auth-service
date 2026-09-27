namespace OstrunAuthService.Domain.Entities;

public sealed class User
{
    private readonly List<Account> _accounts = [];

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public bool EmailVerified { get; private set; }
    public string? Name { get; private set; }
    public string? Image { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyCollection<Account> Accounts => _accounts;

    // Null when the user has no password, e.g. signed up through a social provider.
    public string? PasswordHash =>
        _accounts.SingleOrDefault(a => a.ProviderId == Account.CredentialProviderId)?.PasswordHash;

    private User()
    {
    }

    public static User RegisterWithPassword(string email, string passwordHash, DateTime createdAt)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            CreatedAt = createdAt,
        };
        user._accounts.Add(Account.Credential(user.Id, passwordHash, createdAt));
        return user;
    }

    // The provider has verified the email, so the user starts verified.
    public static User RegisterWithSocialAccount(
        string email, string? name, string? image, string providerId, string providerAccountId, DateTime createdAt)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            EmailVerified = true,
            Name = name,
            Image = image,
            CreatedAt = createdAt,
        };
        user._accounts.Add(Account.Social(user.Id, providerId, providerAccountId, createdAt));
        return user;
    }

    // Only for a provider identity whose email the provider has verified and
    // that matches this user's email, which also verifies it for this user.
    public void LinkSocialAccount(string providerId, string providerAccountId, DateTime linkedAt)
    {
        _accounts.Add(Account.Social(Id, providerId, providerAccountId, linkedAt));
        EmailVerified = true;
    }

    // Emails are stored lowercased so the unique index on users.Email also
    // rejects case variants of an existing address.
    public static string NormalizeEmail(string email) => email.ToLowerInvariant();
}
