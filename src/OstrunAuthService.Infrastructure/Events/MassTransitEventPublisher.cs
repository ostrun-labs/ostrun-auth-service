using MassTransit;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Events;

public sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
{
    public Task PublishUserRegisteredAsync(User user, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(
            new UserRegistered
            {
                UserId = user.Id,
                Email = user.Email,
                RegisteredAt = user.CreatedAt,
            },
            cancellationToken);

    public Task PublishUserLoggedInAsync(User user, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(
            new UserLoggedIn
            {
                UserId = user.Id,
                Email = user.Email,
                LoggedInAt = DateTime.UtcNow,
            },
            cancellationToken);
}
