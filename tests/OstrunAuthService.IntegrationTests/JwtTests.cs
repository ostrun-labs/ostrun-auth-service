using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace OstrunAuthService.IntegrationTests;

// Plays a consuming service: it only knows the JWKS, never the private key.
[Collection(ApiCollection.Name)]
public class JwtTests(AuthApiFactory factory)
{
    private const string Password = "Password123!";
    private readonly HttpClient _client = factory.CreateHttpsClient();

    [Fact]
    public async Task Jwks_PublishesOnePublicRsaKeyWithoutPrivateParameters()
    {
        var jwks = await _client.GetFromJsonAsync<JsonElement>("/auth/.well-known/jwks.json");

        var key = Assert.Single(jwks.GetProperty("keys").EnumerateArray());
        Assert.Equal(["alg", "e", "kid", "kty", "n", "use"], key.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
    }

    [Fact]
    public async Task LoginToken_VerifiesWithTheJwksKeyAndCarriesItsKid()
    {
        var (token, email) = await LoginToken();
        var (publicKey, kid) = await JwksKey();

        var principal = Validate(token, publicKey);

        Assert.Equal(kid, new JwtSecurityTokenHandler().ReadJwtToken(token).Header.Kid);
        Assert.Equal(email, principal.FindFirst("email")?.Value);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsRejectedByTheJwksKey()
    {
        var (publicKey, kid) = await JwksKey();
        using var attackerKey = RSA.Create(2048);
        var forged = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: AuthApiFactory.Issuer,
            audience: AuthApiFactory.Audience,
            claims: [new("sub", Guid.NewGuid().ToString()), new("email", "admin@ostrun.dev")],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(attackerKey) { KeyId = kid }, SecurityAlgorithms.RsaSha256)));

        Assert.ThrowsAny<SecurityTokenInvalidSignatureException>(() => Validate(forged, publicKey));
    }

    public static TheoryData<string, string> BadSigningKeys()
    {
        using var rsa2048 = RSA.Create(2048);
        using var rsa1024 = RSA.Create(1024);
        return new TheoryData<string, string>
        {
            { Base64(rsa2048.ExportSubjectPublicKeyInfoPem()), "must be a private key" },
            { Base64(rsa1024.ExportPkcs8PrivateKeyPem()), "at least 2048 bits" },
            { "change-me-to-a-long-random-secret", "base64-encoded PEM RSA private key" },
        };
    }

    [Theory]
    [MemberData(nameof(BadSigningKeys))]
    public void Startup_WithABadSigningKey_FailsWithAClearError(string signingKey, string expectedError)
    {
        using var _ = new EnvironmentOverride(("Jwt__SigningKey", signingKey));
        using var misconfigured = new WebApplicationFactory<Program>();

        var error = Record.Exception(() => misconfigured.CreateClient());

        Assert.NotNull(error);
        Assert.Contains(expectedError, error.ToString());
    }

    private async Task<(string Token, string Email)> LoginToken()
    {
        var email = $"{Guid.NewGuid():N}@it.ostrun.dev";
        await _client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        var login = await _client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("token").GetString()!, email);
    }

    private async Task<(RsaSecurityKey Key, string Kid)> JwksKey()
    {
        var jwks = await _client.GetFromJsonAsync<JsonElement>("/auth/.well-known/jwks.json");
        var jwk = jwks.GetProperty("keys")[0];
        var kid = jwk.GetProperty("kid").GetString()!;
        var key = new RsaSecurityKey(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("n").GetString()),
            Exponent = Base64UrlEncoder.DecodeBytes(jwk.GetProperty("e").GetString()),
        })
        { KeyId = kid };
        return (key, kid);
    }

    private static System.Security.Claims.ClaimsPrincipal Validate(string token, SecurityKey publicKey) =>
        new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = AuthApiFactory.Issuer,
            ValidAudience = AuthApiFactory.Audience,
            IssuerSigningKey = publicKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        }, out _);

    private static string Base64(string pem) => Convert.ToBase64String(Encoding.UTF8.GetBytes(pem));
}
