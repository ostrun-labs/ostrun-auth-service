using Microsoft.EntityFrameworkCore;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Persistence;

public sealed class UserRepository(AuthDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.Include(u => u.Accounts).SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);
}
