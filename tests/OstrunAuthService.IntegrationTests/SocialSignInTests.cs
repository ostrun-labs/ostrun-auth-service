using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OstrunAuthService.Application.Auth;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Application.Social;
using OstrunAuthService.Infrastructure.Persistence;

namespace OstrunAuthService.IntegrationTests;

// Exercises the sign-in rules against real Postgres. The HTTP endpoints that
// feed them from Google and GitHub come in a later change.
[Collection(ApiCollection.Name)]
public class SocialSignInTests(AuthApiFactory factory)
{
    private static readonly ClientInfo Client = new("203.0.113.7", "integration-test");

    [Fact]
    public async Task NewIdentity_CreatesAVerifiedUserWithOnlyThatAccount()
    {
        var email = UniqueEmail();

        await SignIn(Identity(email));

        var user = await WithDb(db => db.Users.Include(u => u.Accounts).SingleAsync(u => u.Email == email));
        Assert.True(user.EmailVerified);
        Assert.Equal("Ada", user.Name);
        var account = Assert.Single(user.Accounts);
        Assert.Equal(("google", "sub-" + email), (account.ProviderId, account.ProviderAccountId));
        Assert.Null(account.PasswordHash);
    }

    [Fact]
    public async Task SameIdentityTwice_ReusesTheUserAndStartsTwoSessions()
    {
        var email = UniqueEmail();

        await SignIn(Identity(email));
        await SignIn(Identity(email));

        Assert.Equal(1, await WithDb(db => db.Users.CountAsync(u => u.Email == email)));
        Assert.Equal(2, await WithDb(db => db.Sessions.CountAsync(s => s.User.Email == email)));
    }

    [Fact]
    public async Task VerifiedEmailOfPasswordUser_LinksAndKeepsThePasswordWorking()
    {
        var email = UniqueEmail();
        var client = factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/auth/register", new { email, password = "Password123!" });

        await SignIn(Identity(email.ToUpperInvariant()));

        var user = await WithDb(db => db.Users.Include(u => u.Accounts).SingleAsync(u => u.Email == email));
        Assert.Equal(["credential", "google"], user.Accounts.Select(a => a.ProviderId).Order());
        Assert.True(user.EmailVerified);
        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = "Password123!" });
        Assert.True(login.IsSuccessStatusCode);
    }

    [Fact]
    public async Task UnverifiedEmailOfPasswordUser_IsRefusedAndNothingIsSaved()
    {
        var email = UniqueEmail();
        await factory.CreateHttpsClient().PostAsJsonAsync("/auth/register", new { email, password = "Password123!" });

        await Assert.ThrowsAsync<SocialEmailNotVerifiedException>(() => SignIn(Identity(email) with { EmailVerified = false }));

        var userId = await WithDb(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        Assert.Equal(1, await WithDb(db => db.Accounts.CountAsync(a => a.UserId == userId)));
        Assert.Equal(0, await WithDb(db => db.Sessions.CountAsync(s => s.User.Email == email)));
    }

    private async Task SignIn(ExternalIdentity identity)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SocialSignInService>().SignInAsync(identity, Client, CancellationToken.None);
    }

    private async Task<T> WithDb<T>(Func<AuthDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AuthDbContext>());
    }

    private static ExternalIdentity Identity(string email) =>
        new("google", "sub-" + email.ToLowerInvariant(), email, EmailVerified: true, "Ada", "https://img.example/ada.png");

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@it.ostrun.dev";
}
