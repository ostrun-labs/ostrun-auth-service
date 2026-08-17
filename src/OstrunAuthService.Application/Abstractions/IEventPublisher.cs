using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Abstractions;

// Contract kept broker-agnostic on purpose: no message broker has been chosen yet for
// Ostrun (see ostrun/contracts/README.md). The Infrastructure implementation is a
// logging stub until that decision is made.
public interface IEventPublisher
{
    Task PublishUserRegisteredAsync(User user, CancellationToken cancellationToken);

    Task PublishUserLoggedInAsync(User user, CancellationToken cancellationToken);
}
