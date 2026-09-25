namespace OstrunAuthService.Application.Abstractions;

// Commits staged entities and published events in one transaction: events go
// to an outbox table and are delivered to the broker after the commit.
public interface IUnitOfWork
{
    /// <exception cref="Exceptions.EmailAlreadyRegisteredException">A staged user's email is already taken.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
