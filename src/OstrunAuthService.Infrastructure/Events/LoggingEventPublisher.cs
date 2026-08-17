using Microsoft.Extensions.Logging;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Events;

// Placeholder implementation: Ostrun has not chosen a message broker yet, so
// publishing just logs the event name/payload instead of emitting it anywhere.
// Swap this for a real publisher (e.g. an outbox + broker client) once that
// decision is made, without touching Application or Api.
public sealed class LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) : IEventPublisher
{
    public Task PublishUserRegisteredAsync(User user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Event Ostrun.Auth.UserRegistered: {UserId} {Email}", user.Id, user.Email);
        return Task.CompletedTask;
    }

    public Task PublishUserLoggedInAsync(User user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Event Ostrun.Auth.UserLoggedIn: {UserId} {Email}", user.Id, user.Email);
        return Task.CompletedTask;
    }
}
