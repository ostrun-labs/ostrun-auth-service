using OstrunAuthService.Application.Auth;

namespace OstrunAuthService.Api.Endpoints;

// Scoped to /auth so the browser only sends it to this service, never to the
// other services behind the gateway. Lax blocks it on cross-site POSTs.
internal static class SessionCookie
{
    private const string Name = "ostrun_auth_session";

    private static readonly CookieOptions Options = new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/auth",
    };

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Set(HttpResponse response, IssuedSession session) =>
        response.Cookies.Append(Name, session.Token, new CookieOptions(Options) { Expires = session.ExpiresAtUtc });

    public static void Clear(HttpResponse response) => response.Cookies.Delete(Name, Options);
}
