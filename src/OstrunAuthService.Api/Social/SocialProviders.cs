namespace OstrunAuthService.Api.Social;

public sealed class OAuthProviderSettings
{
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public static class SocialProviders
{
    // Provider id (in URLs and accounts.ProviderId) mapped to its
    // configuration section. A provider is enabled when its section has both
    // a client id and a client secret.
    public static readonly IReadOnlyDictionary<string, string> ConfigSections = new Dictionary<string, string>
    {
        ["google"] = "Google",
    };

    public static IServiceCollection AddSocialProviderSettings(this IServiceCollection services)
    {
        foreach (var (providerId, section) in ConfigSections)
        {
            services.AddOptions<OAuthProviderSettings>(providerId)
                .BindConfiguration(section)
                .Validate(
                    s => string.IsNullOrWhiteSpace(s.ClientId) == string.IsNullOrWhiteSpace(s.ClientSecret),
                    $"Set both {section}__ClientId and {section}__ClientSecret to enable {providerId} sign-in, or neither to disable it.")
                .ValidateOnStart();
        }

        return services;
    }
}
