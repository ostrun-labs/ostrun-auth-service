using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OstrunAuthService.Infrastructure.Security;

// Published at the JWKS endpoint. Only public RSA parameters exist on this
// type, so the private key can't leak through serialization.
public sealed record PublicJsonWebKey(string Kty, string Use, string Alg, string Kid, string N, string E);

public sealed class JwtSigningKey
{
    public const string Algorithm = SecurityAlgorithms.RsaSha256;
    private const int MinimumKeySizeInBits = 2048;

    public RsaSecurityKey PrivateKey { get; }
    public RsaSecurityKey PublicKey { get; }
    public PublicJsonWebKey PublicJwk { get; }

    private JwtSigningKey(RSAParameters privateParameters, RSAParameters publicParameters)
    {
        PublicKey = new RsaSecurityKey(publicParameters);
        // RFC 7638 thumbprint: stable for a given key, so consumers can cache
        // by kid and a rotated key gets a new kid without extra config.
        var keyId = Base64UrlEncoder.Encode(PublicKey.ComputeJwkThumbprint());
        PublicKey.KeyId = keyId;
        PrivateKey = new RsaSecurityKey(privateParameters) { KeyId = keyId };
        PublicJwk = new PublicJsonWebKey(
            "RSA",
            "sig",
            Algorithm,
            keyId,
            Base64UrlEncoder.Encode(publicParameters.Modulus),
            Base64UrlEncoder.Encode(publicParameters.Exponent));
    }

    /// <summary>Parses a base64-encoded PEM RSA private key (PKCS#1 or PKCS#8).</summary>
    /// <exception cref="FormatException">The value is not a usable RSA private key.</exception>
    public static JwtSigningKey FromBase64Pem(string base64Pem)
    {
        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(base64Pem)));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or CryptographicException)
        {
            throw new FormatException("must be a base64-encoded PEM RSA private key.", ex);
        }

        if (rsa.KeySize < MinimumKeySizeInBits)
        {
            throw new FormatException($"RSA key must be at least {MinimumKeySizeInBits} bits, got {rsa.KeySize}.");
        }

        RSAParameters privateParameters;
        try
        {
            privateParameters = rsa.ExportParameters(includePrivateParameters: true);
        }
        catch (CryptographicException ex)
        {
            throw new FormatException("must be a private key, got a public key.", ex);
        }

        return new JwtSigningKey(privateParameters, rsa.ExportParameters(includePrivateParameters: false));
    }
}
