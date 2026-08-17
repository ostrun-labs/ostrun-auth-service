using System.Text.RegularExpressions;
using OstrunAuthService.Application.Auth;

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

        group.MapPost("/login", async (LoginUserRequest request, AuthService authService, CancellationToken cancellationToken) =>
        {
            var validationError = Validate(request.Email, request.Password);
            if (validationError is not null)
            {
                return Results.ValidationProblem(validationError);
            }

            var result = await authService.LoginAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        return app;
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
