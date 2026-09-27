using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;

namespace OstrunAuthService.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RegisterAndLoginTests(AuthApiFactory factory)
{
    private const string Password = "Password123!";
    private readonly HttpClient _client = factory.CreateHttpsClient();

    [Fact]
    public async Task Register_ThenLogin_ReturnsRs256TokenForTheUser()
    {
        var email = UniqueEmail();

        var register = await Register(email);
        var login = await Login(email);

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<TokenResponse>();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body!.Token);
        Assert.Equal("RS256", jwt.Header.Alg);
        Assert.Equal(email, jwt.Payload["email"]);
    }

    [Fact]
    public async Task Register_WithDifferentCasing_IsTheSameAccount()
    {
        var email = UniqueEmail();

        await Register(email.ToUpperInvariant());
        var duplicate = await Register(email);
        var login = await Login(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Register_ConcurrentlyWithSameEmail_CreatesOneAccountAndConflictsTheRest()
    {
        var email = UniqueEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Register(email)));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(19, statuses.Count(s => s == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownEmail_Returns401()
    {
        var email = UniqueEmail();
        await Register(email);

        var wrongPassword = await _client.PostAsJsonAsync("/auth/login", new { email, password = "WrongPass1!" });
        var unknownEmail = await Login(UniqueEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidInput_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new { email = "not-an-email", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<HttpResponseMessage> Register(string email) =>
        _client.PostAsJsonAsync("/auth/register", new { email, password = Password });

    private Task<HttpResponseMessage> Login(string email) =>
        _client.PostAsJsonAsync("/auth/login", new { email, password = Password });

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@it.ostrun.dev";

    private sealed record TokenResponse(string Token, DateTime ExpiresAtUtc);
}
