using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using OstrunAuthService.Infrastructure.Security;

namespace OstrunAuthService.UnitTests.Security;

public class JwtSettingsTests
{
    public static TheoryData<string, bool> SigningKeys()
    {
        using var rsa2048 = RSA.Create(2048);
        using var rsa1024 = RSA.Create(1024);
        return new TheoryData<string, bool>
        {
            { Base64(rsa2048.ExportPkcs8PrivateKeyPem()), true },
            { Base64(rsa2048.ExportRSAPrivateKeyPem()), true },
            { Base64(rsa2048.ExportSubjectPublicKeyInfoPem()), false },
            { Base64(rsa1024.ExportPkcs8PrivateKeyPem()), false },
            { rsa2048.ExportPkcs8PrivateKeyPem(), false },
            { "change-me-to-a-long-random-secret", false },
        };
    }

    [Theory]
    [MemberData(nameof(SigningKeys))]
    public void SigningKey_MustBeBase64PemRsaPrivateKeyOfAtLeast2048Bits(string signingKey, bool expectedValid)
    {
        var settings = new JwtSettings { SigningKey = signingKey, Issuer = "issuer", Audience = "audience" };

        var isValid = Validator.TryValidateObject(settings, new ValidationContext(settings), null, validateAllProperties: true);

        Assert.Equal(expectedValid, isValid);
    }

    internal static string Base64(string pem) => Convert.ToBase64String(Encoding.UTF8.GetBytes(pem));
}
