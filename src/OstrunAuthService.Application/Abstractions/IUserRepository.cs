using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.EmailAlreadyRegisteredException">The email is already taken.</exception>
    Task AddAsync(User user, CancellationToken cancellationToken);
}
