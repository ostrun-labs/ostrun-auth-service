using Microsoft.EntityFrameworkCore;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Persistence;

public sealed class SessionRepository(AuthDbContext dbContext) : ISessionRepository
{
    public Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.Sessions.Include(s => s.User).SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

    public void Add(Session session) => dbContext.Sessions.Add(session);

    public void Remove(Session session) => dbContext.Sessions.Remove(session);
}
