using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Auth;
using OstrunAuthService.Infrastructure.Events;
using OstrunAuthService.Infrastructure.Persistence;
using OstrunAuthService.Infrastructure.Security;

namespace OstrunAuthService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuthDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Auth")));

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(HttpEventPublisher.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5));

        var subscriptions = EventSubscriptionsParser.Parse(configuration["Events:Subscriptions"]);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEventPublisher>(sp => new HttpEventPublisher(
            sp.GetRequiredService<IHttpClientFactory>(),
            subscriptions,
            sp.GetRequiredService<ILogger<HttpEventPublisher>>()));
        services.AddScoped<AuthService>();

        return services;
    }
}
