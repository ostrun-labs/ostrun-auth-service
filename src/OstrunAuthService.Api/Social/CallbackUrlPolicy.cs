namespace OstrunAuthService.Api.Social;

public sealed class AuthSettings
{
    public const string SectionName = "Auth";

    // Comma-separated origins (scheme://host[:port]) that may receive the
    // browser after social sign-in, e.g. "https://app.example.com".
    public string? TrustedOrigins { get; init; }
}

// Guards the post-sign-in redirect so the service can't be used as an open
// redirect to an attacker's site.
public static class CallbackUrlPolicy
{
    public static bool IsAllowed(string callbackUrl, AuthSettings settings)
    {
        if (callbackUrl.StartsWith('/'))
        {
            // "//evil.com" and "/\evil.com" are protocol-relative URLs to another host.
            return callbackUrl.Length == 1 || (callbackUrl[1] != '/' && callbackUrl[1] != '\\');
        }

        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        var origin = uri.GetLeftPart(UriPartial.Authority);
        return (settings.TrustedOrigins ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(trusted => string.Equals(trusted.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
    }
}
