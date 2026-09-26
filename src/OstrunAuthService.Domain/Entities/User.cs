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
}
