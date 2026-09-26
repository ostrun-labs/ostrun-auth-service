using Microsoft.AspNetCore.Identity;
using OstrunAuthService.Application.Abstractions;

namespace OstrunAuthService.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _inner = new();

    // ASP.NET Core Identity's hasher ignores the user argument for the default
    // (v3) algorithm, so a placeholder object stands in for it.
    public string Hash(string password) => _inner.HashPassword(PlaceholderUser, password);

    public bool Verify(string passwordHash, string providedPassword) =>
        _inner.VerifyHashedPassword(PlaceholderUser, passwordHash, providedPassword) != PasswordVerificationResult.Failed;

    public void SimulateVerify(string providedPassword) => Verify(_dummyHash.Value, providedPassword);

    private static readonly object PlaceholderUser = new();

    private readonly Lazy<string> _dummyHash = new(() => new PasswordHasher<object>().HashPassword(PlaceholderUser, Guid.NewGuid().ToString()));
}
