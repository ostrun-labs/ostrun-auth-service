using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    // Staged until IUnitOfWork.SaveChangesAsync.
    void Add(User user);
}
