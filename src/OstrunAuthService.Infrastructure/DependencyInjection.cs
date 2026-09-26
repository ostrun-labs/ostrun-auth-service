using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        var rabbitMq = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            // Publishes are written to the outbox tables on the next
            // AuthDbContext.SaveChangesAsync and sent to the broker by a
            // background delivery service, so an event is never lost when the
            // broker is down and never sent for a rolled-back transaction.
            x.AddEntityFrameworkOutbox<AuthDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            if (!string.IsNullOrWhiteSpace(rabbitMq.Host))
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitMq.Host, rabbitMq.VirtualHost, h =>
                    {
                        h.Username(rabbitMq.Username);
                        h.Password(rabbitMq.Password);
                    });

                    // Plain JSON on the wire, no MassTransit envelope. The
                    // envelope embeds the publisher's CLR type URN
                    // (OstrunAuthService.Infrastructure.Events:UserRegistered),
                    // which a consumer's differently-named local type can
                    // never match, by design. AnyMessageType additionally
                    // drops the MT-MessageType transport header the raw
                    // serializer still stamps by default, which a consumer
                    // otherwise still uses to reject a structurally-identical
                    // message from a differently-named type (observed during
                    // the spike: it landed in RabbitMQ's default "_skipped"
                    // queue with the payload intact but unrouted). Raw JSON
                    // also interops with a future non-.NET consumer with no
                    // MassTransit dependency at all.
                    cfg.UseRawJsonSerializer(RawSerializerOptions.AnyMessageType | RawSerializerOptions.AddTransportHeaders | RawSerializerOptions.CopyHeaders);

                    // Exchange names are forced to the Ostrun contract name
                    // (ostrun/contracts/ostrun/auth/*.v1.json) instead of the
                    // CLR type name, so a consumer in any stack can bind to
                    // it without sharing this type.
                    cfg.Message<UserRegistered>(m => m.SetEntityName("Ostrun.Auth.UserRegistered"));
                    cfg.Message<UserLoggedIn>(m => m.SetEntityName("Ostrun.Auth.UserLoggedIn"));

                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                // Standalone dev default: no RabbitMq:Host configured, so this
                // service still runs alone with no broker.
                x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            }
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton(sp => JwtSigningKey.FromBase64Pem(sp.GetRequiredService<IOptions<JwtSettings>>().Value.SigningKey));
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        services.AddScoped<AuthService>();

        return services;
    }
}
