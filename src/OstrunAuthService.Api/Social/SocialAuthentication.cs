using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace OstrunAuthService.Api.Social;

public static class SocialAuthentication
{
    // Holds the provider's profile between the provider callback and
    // /auth/callback/{provider}/complete, then is deleted.
    public const string ExternalScheme = "External";

    public const string EmailVerifiedClaim = "email_verified";
    public const string PictureClaim = "picture";
    public const string CallbackUrlItem = "callbackURL";
    public const string ProviderItem = "provider";

    // Remote handlers inspect every request, and fail when their client id is
    // missing, so a provider is only registered when it's configured.
    public static AuthenticationBuilder AddSocialProviders(this AuthenticationBuilder authentication, IConfiguration configuration)
    {
        authentication.AddCookie(ExternalScheme, options =>
        {
            options.Cookie.Name = "ostrun_auth_external";
            options.Cookie.Path = "/auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        });

        var google = configuration.GetSection(SocialProviders.ConfigSections["google"]).Get<OAuthProviderSettings>();
        if (google?.IsConfigured == true)
        {
            authentication.AddGoogle("google", options =>
            {
                options.ClientId = google.ClientId!;
                options.ClientSecret = google.ClientSecret!;
                options.SignInScheme = ExternalScheme;
                options.CallbackPath = "/auth/callback/google";
                options.CorrelationCookie.Path = "/auth";
                options.ClaimActions.MapJsonKey(PictureClaim, "picture");
                options.ClaimActions.MapCustomJson(EmailVerifiedClaim, user =>
                    user.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.True ? "true" : "false");
                options.Events.OnRemoteFailure = RedirectWithError;
            });
        }

        return authentication;
    }

    // The user denied consent, or the state or correlation check failed:
    // send them back to the app with an error instead of a 500.
    private static Task RedirectWithError(RemoteFailureContext context)
    {
        var callbackUrl = context.Properties?.GetString(CallbackUrlItem) ?? "/";
        var error = context.Request.Query["error"].FirstOrDefault() == "access_denied" ? "access_denied" : "sign_in_failed";
        context.Response.Redirect(QueryHelpers.AddQueryString(callbackUrl, "error", error));
        context.HandleResponse();
        return Task.CompletedTask;
    }
}
