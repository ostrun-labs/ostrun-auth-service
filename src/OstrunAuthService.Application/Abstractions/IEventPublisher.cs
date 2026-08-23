using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Abstractions;

// Contract kept broker-agnostic on purpose: the Infrastructure implementation
// (MassTransitEventPublisher) can be swapped for a different transport
// without touching Application or Api.
public interface IEventPublisher
{
    Task PublishUserRegisteredAsync(User user, CancellationToken cancellationToken);

    Task PublishUserLoggedInAsync(User user, CancellationToken cancellationToken);
}
