using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OstrunAuthService.Api.Social;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Application.Social;

namespace OstrunAuthService.Api.Endpoints;

public static class SocialEndpoints
{
    public static IEndpointRouteBuilder MapSocialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapGet("/sign-in/social/{provider}", async (string provider, string? callbackURL, IAuthenticationSchemeProvider schemes, IOptions<AuthSettings> settings) =>
        {
            if (!SocialProviders.ConfigSections.ContainsKey(provider) || await schemes.GetSchemeAsync(provider) is null)
            {
                return Results.NotFound();
            }

            var callbackUrl = callbackURL ?? "/";
            if (!CallbackUrlPolicy.IsAllowed(callbackUrl, settings.Value))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["callbackURL"] = ["Must be a path on this site or an origin listed in Auth__TrustedOrigins."],
                });
            }

            var properties = new AuthenticationProperties { RedirectUri = $"/auth/callback/{provider}/complete" };
            properties.Items[SocialAuthentication.CallbackUrlItem] = callbackUrl;
            properties.Items[SocialAuthentication.ProviderItem] = provider;
            return Results.Challenge(properties, [provider]);
        });

        group.MapGet("/callback/{provider}/complete", async (string provider, HttpContext httpContext, SocialSignInService socialSignIn, CancellationToken cancellationToken) =>
        {
            var external = await httpContext.AuthenticateAsync(SocialAuthentication.ExternalScheme);
            await httpContext.SignOutAsync(SocialAuthentication.ExternalScheme);

            var identity = external.Succeeded ? ToIdentity(provider, external) : null;
            if (identity is null)
            {
                return Results.Unauthorized();
            }

            var callbackUrl = external.Properties!.GetString(SocialAuthentication.CallbackUrlItem) ?? "/";
            try
            {
                var outcome = await socialSignIn.SignInAsync(identity, AuthEndpoints.ClientInfoOf(httpContext), cancellationToken);
                SessionCookie.Set(httpContext.Response, outcome.Session);
                return Results.Redirect(callbackUrl);
            }
            catch (SocialEmailNotVerifiedException)
            {
                return Results.Redirect(QueryHelpers.AddQueryString(callbackUrl, "error", "email_not_verified"));
            }
        });

        return app;
    }

    // Null unless the external cookie was issued by this provider's flow and
    // carries the provider's stable user id.
    private static ExternalIdentity? ToIdentity(string provider, AuthenticateResult external)
    {
        var principal = external.Principal!;
        var providerAccountId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (external.Properties?.GetString(SocialAuthentication.ProviderItem) != provider || providerAccountId is null)
        {
            return null;
        }

        return new ExternalIdentity(
            provider,
            providerAccountId,
            principal.FindFirstValue(ClaimTypes.Email),
            principal.FindFirstValue(SocialAuthentication.EmailVerifiedClaim) == "true",
            principal.FindFirstValue(ClaimTypes.Name),
            principal.FindFirstValue(SocialAuthentication.PictureClaim));
    }
}
