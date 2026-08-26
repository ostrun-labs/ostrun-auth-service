namespace OstrunAuthService.Infrastructure.Events;

// Local message shapes for the events this service publishes. Kept local
// rather than in a shared contracts package on purpose: a future Node/other
// stack service must be able to consume these without a C# dependency. The
// RabbitMQ exchange name is forced to the Ostrun contract name in
// DependencyInjection.cs instead of being derived from these CLR type names,
// so the binding doesn't depend on this choice matching a consumer's own
// type name.
public sealed record UserRegistered
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required DateTime RegisteredAt { get; init; }
}

public sealed record UserLoggedIn
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required DateTime LoggedInAt { get; init; }
}
