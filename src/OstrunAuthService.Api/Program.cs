using HealthChecks.NpgSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using OstrunAuthService.Api;
using OstrunAuthService.Api.Endpoints;
using OstrunAuthService.Api.Social;
using OstrunAuthService.Infrastructure;
using OstrunAuthService.Infrastructure.Persistence;
using OstrunAuthService.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSocialProviderSettings();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    .AddSocialProviders(builder.Configuration);

builder.Services.AddOptions<AuthSettings>().BindConfiguration(AuthSettings.SectionName);

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>, JwtSigningKey>((options, settings, signingKey) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey.PublicKey,
            ValidAlgorithms = [JwtSigningKey.Algorithm],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("Auth");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services
        .AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres");
}

var app = builder.Build();

// MVP starter: apply migrations on startup instead of a separate migration
// job/step, since each generated instance is a single dev-owned deployment.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapJwksEndpoint();
app.MapProvidersEndpoint();
app.MapSocialEndpoints();

app.Run();

// Lets the integration tests host the app with WebApplicationFactory<Program>.
public partial class Program;
