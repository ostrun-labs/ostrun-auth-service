using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Abstractions;

public interface ISessionRepository
{
    // Includes the session's user.
    Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    // Staged until IUnitOfWork.SaveChangesAsync.
    void Add(Session session);

    // Staged until IUnitOfWork.SaveChangesAsync.
    void Remove(Session session);
}
