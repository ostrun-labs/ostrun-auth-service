using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OstrunAuthService.Infrastructure.Persistence;

namespace OstrunAuthService.IntegrationTests;

// Drives the real redirect flow: our sign-in endpoint, the redirect to
// Google, Google's return to /auth/callback/google, and the final redirect to
// the app. Google's token and userinfo endpoints are faked.
[Collection(ApiCollection.Name)]
public class GoogleSignInTests(GoogleSignInTests.GoogleEnabledApp app) : IClassFixture<GoogleSignInTests.GoogleEnabledApp>
{
    [Fact]
    public async Task NewGoogleUser_LandsOnTheCallbackUrlSignedIn()
    {
        var client = app.CreateClient();
        var email = UniqueEmail();

        var (start, final) = await RunFlow(client, "/after", Profile(email));

        Assert.Equal("/after", final.Headers.Location!.OriginalString);
        var authorize = QueryHelpers.ParseQuery(start.Headers.Location!.Query);
        Assert.Equal("https://localhost/auth/callback/google", authorize["redirect_uri"]);
        Assert.Equal("test-google-client", authorize["client_id"]);
        Assert.Contains("email", authorize["scope"].ToString());
        Assert.Contains("code=fake-code", app.Google.LastTokenRequest);
        var session = await client.GetFromJsonAsync<JsonObject>("/auth/session");
        Assert.Equal(email, session!["user"]!["email"]!.GetValue<string>());
        Assert.Equal("Ada Lovelace", session["user"]!["name"]!.GetValue<string>());
        Assert.True(session["user"]!["emailVerified"]!.GetValue<bool>());
    }

    [Fact]
    public async Task SameGoogleAccountTwice_ReusesTheUser()
    {
        var email = UniqueEmail();

        await RunFlow(app.CreateClient(), "/", Profile(email));
        await RunFlow(app.CreateClient(), "/", Profile(email));

        Assert.Equal(1, await app.WithDb(db => db.Users.CountAsync(u => u.Email == email)));
    }

    [Fact]
    public async Task UnverifiedGoogleEmail_RedirectsWithAnErrorAndNoSession()
    {
        var client = app.CreateClient();

        var (_, final) = await RunFlow(client, "/after", Profile(UniqueEmail(), emailVerified: false));

        Assert.Equal("/after?error=email_not_verified", final.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/session")).StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example/steal")]
    [InlineData("//evil.example/steal")]
    [InlineData("/\\evil.example/steal")]
    [InlineData("javascript:alert(1)")]
    public async Task SignIn_WithUntrustedCallbackUrl_Returns400(string callbackUrl)
    {
        var response = await app.CreateClient().GetAsync($"/auth/sign-in/social/google?callbackURL={Uri.EscapeDataString(callbackUrl)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_WithTrustedOriginCallbackUrl_RedirectsToGoogle()
    {
        var response = await app.CreateClient().GetAsync($"/auth/sign-in/social/google?callbackURL={Uri.EscapeDataString("https://app.example.com/home")}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("fake-google.test", response.Headers.Location!.Host);
    }

    [Fact]
    public async Task SignIn_WithUnknownProvider_Returns404()
    {
        var response = await app.CreateClient().GetAsync("/auth/sign-in/social/facebook");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Callback_WithTamperedState_RedirectsWithAnError()
    {
        var client = app.CreateClient();
        await client.GetAsync("/auth/sign-in/social/google?callbackURL=/after");

        var callback = await client.GetAsync("/auth/callback/google?code=fake-code&state=tampered");

        Assert.Equal("/?error=sign_in_failed", callback.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Callback_WhenTheUserDeniesConsent_RedirectsWithAccessDenied()
    {
        var client = app.CreateClient();
        var start = await client.GetAsync("/auth/sign-in/social/google?callbackURL=/after");
        var state = QueryHelpers.ParseQuery(start.Headers.Location!.Query)["state"];

        var callback = await client.GetAsync($"/auth/callback/google?error=access_denied&state={Uri.EscapeDataString(state!)}");

        Assert.Equal("/after?error=access_denied", callback.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Complete_WithoutAGoogleSignInInProgress_Returns401()
    {
        var response = await app.CreateClient().GetAsync("/auth/callback/google/complete");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_WhenGoogleIsNotConfigured_Returns404()
    {
        var response = await app.Shared.CreateHttpsClient().GetAsync("/auth/sign-in/social/google");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(HttpResponseMessage Start, HttpResponseMessage Final)> RunFlow(HttpClient client, string callbackUrl, JsonObject profile)
    {
        app.Google.Profile = profile;

        var start = await client.GetAsync($"/auth/sign-in/social/google?callbackURL={Uri.EscapeDataString(callbackUrl)}");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var state = QueryHelpers.ParseQuery(start.Headers.Location!.Query)["state"];

        var callback = await client.GetAsync($"/auth/callback/google?code=fake-code&state={Uri.EscapeDataString(state!)}");
        Assert.Equal("/auth/callback/google/complete", callback.Headers.Location!.OriginalString);

        var final = await client.GetAsync(callback.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, final.StatusCode);
        return (start, final);
    }

    private static JsonObject Profile(string email, bool emailVerified = true) => new()
    {
        ["sub"] = "google-" + email,
        ["email"] = email,
        ["email_verified"] = emailVerified,
        ["name"] = "Ada Lovelace",
        ["picture"] = "https://lh3.example/ada.png",
    };

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@it.ostrun.dev";

    // A second app instance with Google configured, pointed at a fake Google.
    public sealed class GoogleEnabledApp : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;

        public GoogleEnabledApp(AuthApiFactory shared)
        {
            Shared = shared;
            using var googleConfig = new EnvironmentOverride(
                ("Google__ClientId", "test-google-client"),
                ("Google__ClientSecret", "test-google-secret"),
                ("Auth__TrustedOrigins", "https://app.example.com"));

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.PostConfigure<GoogleOptions>("google", options =>
                {
                    options.AuthorizationEndpoint = "https://fake-google.test/auth";
                    options.TokenEndpoint = "https://fake-google.test/token";
                    options.UserInformationEndpoint = "https://fake-google.test/userinfo";
                    options.Backchannel = new HttpClient(Google);
                })));

            // Build the host while the environment override is active.
            _ = _factory.Server;
        }

        public AuthApiFactory Shared { get; }

        public FakeGoogle Google { get; } = new();

        public HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        public async Task<T> WithDb<T>(Func<AuthDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AuthDbContext>());
        }

        public void Dispose() => _factory.Dispose();
    }

    public sealed class FakeGoogle : HttpMessageHandler
    {
        public JsonObject Profile { get; set; } = [];

        public string LastTokenRequest { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/token":
                    LastTokenRequest = await request.Content!.ReadAsStringAsync(cancellationToken);
                    return Json(new JsonObject { ["access_token"] = "fake-access-token", ["token_type"] = "Bearer", ["expires_in"] = 3600 });
                case "/userinfo":
                    return Json(Profile);
                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private static HttpResponseMessage Json(JsonObject body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
