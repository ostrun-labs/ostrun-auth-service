using System.Security.Cryptography;
using System.Text;

namespace OstrunAuthService.Domain.Entities;

// A signed-in browser. Only a hash of the token is stored, so a leaked
// sessions table can't be replayed as cookies.
public sealed class Session
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private Session()
    {
    }

    public static (Session Session, string Token) Start(User user, DateTime now, string? ipAddress, string? userAgent)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = HashToken(token),
            ExpiresAt = now + Lifetime,
            CreatedAt = now,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        };

        return (session, token);
    }

    public bool IsActive(DateTime now) => ExpiresAt > now;

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
