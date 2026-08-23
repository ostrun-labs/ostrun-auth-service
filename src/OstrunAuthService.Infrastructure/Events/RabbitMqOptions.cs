namespace OstrunAuthService.Infrastructure.Events;

// Absent Host is the standalone-dev default: DependencyInjection falls back
// to MassTransit's in-memory transport instead of requiring a broker just to
// run this service alone (see ostrun/brainstorm/to_think_about.md).
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string? Host { get; init; }
    public string VirtualHost { get; init; } = "/";
    public string Username { get; init; } = "guest";
    public string Password { get; init; } = "guest";
}
