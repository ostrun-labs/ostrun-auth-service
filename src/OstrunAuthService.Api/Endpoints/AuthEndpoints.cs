using System.Text.RegularExpressions;
using OstrunAuthService.Application.Auth;
using OstrunAuthService.Application.Sessions;

namespace OstrunAuthService.Api.Endpoints;

public static partial class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (RegisterUserRequest request, AuthService authService, CancellationToken cancellationToken) =>
        {
            var validationError = Validate(request.Email, request.Password);
            if (validationError is not null)
            {
                return Results.ValidationProblem(validationError);
            }

            var result = await authService.RegisterAsync(request, cancellationToken);
            return Results.Created($"/auth/users/{result.UserId}", result);
        });

        group.MapPost("/login", async (LoginUserRequest request, AuthService authService, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var validationError = Validate(request.Email, request.Password);
            if (validationError is not null)
            {
                return Results.ValidationProblem(validationError);
            }

            var outcome = await authService.LoginAsync(request, ClientInfoOf(httpContext), cancellationToken);
            SessionCookie.Set(httpContext.Response, outcome.Session);
            return Results.Ok(outcome.AccessToken);
        });

        group.MapGet("/session", async (SessionService sessionService, HttpRequest request, CancellationToken cancellationToken) =>
        {
            var current = await sessionService.GetCurrentAsync(SessionCookie.Read(request), cancellationToken);
            return current is null ? Results.Unauthorized() : Results.Ok(current);
        });

        group.MapPost("/token", async (SessionService sessionService, HttpRequest request, CancellationToken cancellationToken) =>
        {
            var accessToken = await sessionService.IssueAccessTokenAsync(SessionCookie.Read(request), cancellationToken);
            return accessToken is null ? Results.Unauthorized() : Results.Ok(accessToken);
        });

        group.MapPost("/sign-out", async (SessionService sessionService, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            await sessionService.SignOutAsync(SessionCookie.Read(httpContext.Request), cancellationToken);
            SessionCookie.Clear(httpContext.Response);
            return Results.NoContent();
        });

        return app;
    }

    internal static ClientInfo ClientInfoOf(HttpContext httpContext)
    {
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return new ClientInfo(
            httpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent.Length == 0 ? null : userAgent[..Math.Min(userAgent.Length, 512)]);
    }

    private static Dictionary<string, string[]>? Validate(string email, string password)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(email) || !EmailRegex().IsMatch(email))
        {
            errors["email"] = ["A valid email address is required."];
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            errors["password"] = ["Password must be at least 8 characters long."];
        }

        return errors.Count > 0 ? errors : null;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
