using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Abstractions;

public interface ISessionRepository
{
    // Staged until IUnitOfWork.SaveChangesAsync.
    void Add(Session session);
}
