using Microsoft.AspNetCore.Identity;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> _inner = new();

    // ASP.NET Core Identity's hasher ignores the user argument for the default
    // (v3) algorithm, so a placeholder instance avoids loading a real user here.
    public string Hash(string password) => _inner.HashPassword(PlaceholderUser, password);

    public bool Verify(string passwordHash, string providedPassword) =>
        _inner.VerifyHashedPassword(PlaceholderUser, passwordHash, providedPassword) != PasswordVerificationResult.Failed;

    private static readonly User PlaceholderUser = new(Guid.Empty, string.Empty, string.Empty, DateTime.MinValue);
}
