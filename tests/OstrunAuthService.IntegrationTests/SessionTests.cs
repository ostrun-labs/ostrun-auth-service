using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OstrunAuthService.Domain.Entities;
using OstrunAuthService.Infrastructure.Persistence;

namespace OstrunAuthService.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SessionTests(AuthApiFactory factory)
{
    private const string Password = "Password123!";
    private const string CookieName = "ostrun_auth_session";

    [Fact]
    public async Task Login_SetsAHardenedSessionCookieScopedToAuth()
    {
        var (_, login) = await SignedInClient();

        var setCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith(CookieName + "="));
        var attributes = setCookie.Split(';', StringSplitOptions.TrimEntries).Skip(1).Select(a => a.ToLowerInvariant()).ToList();
        Assert.Contains("path=/auth", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("httponly", attributes);
        Assert.Contains("samesite=lax", attributes);
        var expires = DateTime.Parse(attributes.Single(a => a.StartsWith("expires=")).Split('=', 2)[1]).ToUniversalTime();
        Assert.InRange(expires, DateTime.UtcNow.AddDays(7).AddMinutes(-5), DateTime.UtcNow.AddDays(7).AddMinutes(5));
    }

    [Fact]
    public async Task Login_StoresOnlyTheHashOfTheSessionToken()
    {
        var (_, login) = await SignedInClient();
        var token = CookieValue(login);

        var stored = await WithDb(db => db.Sessions.Where(s => s.TokenHash == Session.HashToken(token)).ToListAsync());
        var rawStored = await WithDb(db => db.Sessions.AnyAsync(s => s.TokenHash == token));

        Assert.Single(stored);
        Assert.False(rawStored);
    }

    [Fact]
    public async Task Session_WithCookie_ReturnsTheSignedInUser()
    {
        var (client, _, email) = await SignedInClientWithEmail();

        var response = await client.GetAsync("/auth/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.Equal(email, body!.User.Email);
    }

    [Fact]
    public async Task Token_WithCookie_ReturnsAJwtForTheSessionUser()
    {
        var (client, _) = await SignedInClient();
        var session = await client.GetFromJsonAsync<SessionResponse>("/auth/session");

        var response = await client.PostAsync("/auth/token", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body!.Token);
        Assert.Equal(session!.User.Id.ToString(), jwt.Subject);
    }

    [Fact]
    public async Task SessionAndToken_WithoutOrWithForgedCookie_Return401()
    {
        var anonymous = factory.CreateHttpsClient();
        var forged = WithCookie("forged-token");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/auth/token", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await forged.GetAsync("/auth/session")).StatusCode);
    }

    [Fact]
    public async Task SignOut_DeletesTheSessionSoTheOldCookieStopsWorking()
    {
        var (client, login) = await SignedInClient();
        var token = CookieValue(login);

        var signOut = await client.PostAsync("/auth/sign-out", null);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Contains(signOut.Headers.GetValues("Set-Cookie"), c => c.StartsWith(CookieName + "=;") && c.Contains("1970"));
        var replay = WithCookie(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.PostAsync("/auth/token", null)).StatusCode);
        Assert.False(await WithDb(db => db.Sessions.AnyAsync(s => s.TokenHash == Session.HashToken(token))));
    }

    [Fact]
    public async Task SignOut_WithoutSession_Returns204()
    {
        var response = await factory.CreateHttpsClient().PostAsync("/auth/sign-out", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SessionAndToken_WithExpiredSession_Return401()
    {
        var (client, login) = await SignedInClient();
        var hash = Session.HashToken(CookieValue(login));

        await WithDb(db => db.Sessions.Where(s => s.TokenHash == hash)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddSeconds(-1))));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/auth/token", null)).StatusCode);
    }

    private async Task<(HttpClient Client, HttpResponseMessage Login)> SignedInClient()
    {
        var (client, login, _) = await SignedInClientWithEmail();
        return (client, login);
    }

    private async Task<(HttpClient Client, HttpResponseMessage Login, string Email)> SignedInClientWithEmail()
    {
        var email = $"{Guid.NewGuid():N}@it.ostrun.dev";
        var client = factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (client, login, email);
    }

    // A client without a cookie jar, sending exactly the given session token.
    private HttpClient WithCookie(string token)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieName}={token}");
        return client;
    }

    private static string CookieValue(HttpResponseMessage login) =>
        login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(CookieName + "=")).Split(';')[0][(CookieName.Length + 1)..];

    private async Task<T> WithDb<T>(Func<AuthDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AuthDbContext>());
    }

    private sealed record SessionResponse(SessionUser User);

    private sealed record SessionUser(Guid Id, string Email);

    private sealed record TokenResponse(string Token);
}
