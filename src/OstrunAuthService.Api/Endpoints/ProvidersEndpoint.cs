using Microsoft.Extensions.Options;
using OstrunAuthService.Api.Social;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Api.Endpoints;

public static class ProvidersEndpoint
{
    // Lets a frontend show only the sign-in buttons this instance supports.
    public static IEndpointRouteBuilder MapProvidersEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/providers", (IOptionsMonitor<OAuthProviderSettings> settings) =>
        {
            var social = SocialProviders.ConfigSections.Keys.Where(id => settings.Get(id).IsConfigured);
            return Results.Ok(new { providers = social.Prepend(Account.CredentialProviderId) });
        });

        return app;
    }
}
