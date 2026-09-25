using OstrunAuthService.Infrastructure.Security;

namespace OstrunAuthService.Api.Endpoints;

public static class JwksEndpoint
{
    // Under the service's /auth base path so a gateway routing /auth/* also
    // exposes it.
    public static IEndpointRouteBuilder MapJwksEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/.well-known/jwks.json", (JwtSigningKey signingKey) =>
            Results.Ok(new { keys = new[] { signingKey.PublicJwk } }));

        return app;
    }
}
