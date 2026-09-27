using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OstrunAuthService.Infrastructure.Persistence;

namespace OstrunAuthService.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ForwardedHeadersTests
{
    private const string Gateway = "10.0.0.5";

    [Fact]
    public async Task FromATrustedProxy_UsesThePublicUrlAndTheClientIp()
    {
        using var app = new App(trustedProxies: "10.0.0.0/8");

        var redirectUri = await GoogleRedirectUri(app, peer: Gateway);
        var sessionIp = await SessionIp(app, peer: Gateway);

        Assert.Equal("https://auth.example.com/auth/callback/google", redirectUri);
        Assert.Equal("203.0.113.9", sessionIp);
    }

    [Fact]
    public async Task FromAnUntrustedCaller_IgnoresTheForwardedHeaders()
    {
        using var app = new App(trustedProxies: "10.0.0.0/8");

        var redirectUri = await GoogleRedirectUri(app, peer: "198.51.100.1");
        var sessionIp = await SessionIp(app, peer: "198.51.100.1");

        Assert.Equal("https://localhost/auth/callback/google", redirectUri);
        Assert.Equal("198.51.100.1", sessionIp);
    }

    [Fact]
    public async Task WithoutTrustedProxies_IgnoresTheForwardedHeaders()
    {
        using var app = new App(trustedProxies: null);

        var redirectUri = await GoogleRedirectUri(app, peer: Gateway);

        Assert.Equal("https://localhost/auth/callback/google", redirectUri);
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("10.0.0.0/33")]
    public void Startup_WithAnInvalidTrustedProxy_FailsWithAClearError(string trustedProxies)
    {
        var error = Record.Exception(() => new App(trustedProxies).Dispose());

        Assert.NotNull(error);
        Assert.Contains($"Auth__TrustedProxies entry '{trustedProxies}'", error.ToString());
    }

    private static async Task<string> GoogleRedirectUri(App app, string peer)
    {
        var response = await app.Send(HttpMethod.Get, "/auth/sign-in/social/google", peer);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return QueryHelpers.ParseQuery(response.Headers.Location!.Query)["redirect_uri"].ToString();
    }

    private static async Task<string?> SessionIp(App app, string peer)
    {
        var email = $"{Guid.NewGuid():N}@it.ostrun.dev";
        await app.Send(HttpMethod.Post, "/auth/register", peer, new { email, password = "Password123!" });
        var login = await app.Send(HttpMethod.Post, "/auth/login", peer, new { email, password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return await app.WithDb(db => db.Sessions.Where(s => s.User.Email == email).Select(s => s.IpAddress).SingleAsync());
    }

    // An app with Google configured (to observe the OAuth redirect_uri) that
    // takes the caller's IP from a test header, since TestServer has no peer.
    private sealed class App : IDisposable
    {
        private const string PeerHeader = "X-Test-Peer";
        private readonly WebApplicationFactory<Program> _factory;

        public App(string? trustedProxies)
        {
            using var config = new EnvironmentOverride(
                ("Google__ClientId", "test-google-client"),
                ("Google__ClientSecret", "test-google-secret"),
                ("Auth__TrustedProxies", trustedProxies));

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services => services.AddTransient<IStartupFilter, PeerFromHeader>()));
            _ = _factory.Server;
        }

        public Task<HttpResponseMessage> Send(HttpMethod method, string path, string peer, object? body = null)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var request = new HttpRequestMessage(method, new Uri("https://localhost" + path));
            request.Headers.Add(PeerHeader, peer);
            request.Headers.Add("X-Forwarded-Proto", "https");
            request.Headers.Add("X-Forwarded-Host", "auth.example.com");
            request.Headers.Add("X-Forwarded-For", "203.0.113.9");
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            return client.SendAsync(request);
        }

        public async Task<T> WithDb<T>(Func<AuthDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AuthDbContext>());
        }

        public void Dispose() => _factory.Dispose();

        private sealed class PeerFromHeader : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers[PeerHeader].ToString());
                    return nextMiddleware(context);
                });
                next(app);
            };
        }
    }
}
