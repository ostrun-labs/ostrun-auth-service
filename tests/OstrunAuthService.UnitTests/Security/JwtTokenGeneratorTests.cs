using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OstrunAuthService.Domain.Entities;
using OstrunAuthService.Infrastructure.Security;

namespace OstrunAuthService.UnitTests.Security;

public class JwtTokenGeneratorTests
{
    private readonly JwtSigningKey _signingKey;
    private readonly JwtTokenGenerator _sut;

    public JwtTokenGeneratorTests()
    {
        using var rsa = RSA.Create(2048);
        var settings = new JwtSettings
        {
            SigningKey = JwtSettingsTests.Base64(rsa.ExportPkcs8PrivateKeyPem()),
            Issuer = "issuer",
            Audience = "audience",
        };
        _signingKey = JwtSigningKey.FromBase64Pem(settings.SigningKey);
        _sut = new JwtTokenGenerator(Options.Create(settings), _signingKey);
    }

    [Fact]
    public void Generate_SignsWithRs256AndTheKidPublishedInTheJwks()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(_sut.Generate(NewUser()).Value);

        Assert.Equal("RS256", token.Header.Alg);
        Assert.Equal(_signingKey.PublicJwk.Kid, token.Header.Kid);
    }

    [Fact]
    public void Generate_TokenVerifiesWithOnlyThePublishedPublicKey()
    {
        var user = NewUser();
        var token = _sut.Generate(user).Value;
        var jwk = _signingKey.PublicJwk;
        var publicKey = new RsaSecurityKey(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(jwk.N),
            Exponent = Base64UrlEncoder.DecodeBytes(jwk.E),
        });

        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = "issuer",
            ValidAudience = "audience",
            IssuerSigningKey = publicKey,
            ValidAlgorithms = ["RS256"],
        }, out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirst("sub")?.Value);
    }

    [Fact]
    public void PublicJwk_SerializesWithoutPrivateParameters()
    {
        var json = JsonSerializer.Serialize(_signingKey.PublicJwk, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var fields = JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name).Order();

        Assert.Equal(["alg", "e", "kid", "kty", "n", "use"], fields);
    }

    private static User NewUser() => User.RegisterWithPassword("user@ostrun.dev", "hashed", DateTime.UtcNow);
}
