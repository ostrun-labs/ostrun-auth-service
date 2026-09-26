using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Persistence;

public sealed class SessionRepository(AuthDbContext dbContext) : ISessionRepository
{
    public void Add(Session session) => dbContext.Sessions.Add(session);
}
