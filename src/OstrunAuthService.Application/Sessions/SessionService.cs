using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Auth;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Sessions;

public sealed class SessionService(
    ISessionRepository sessionRepository,
    IJwtTokenGenerator jwtTokenGenerator)
{
    public async Task<CurrentSessionResult?> GetCurrentAsync(string? token, CancellationToken cancellationToken)
    {
        var session = await FindActiveAsync(token, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var user = session.User;
        return new CurrentSessionResult(
            new SessionUser(user.Id, user.Email, user.EmailVerified, user.Name, user.Image),
            new SessionDetails(session.Id, session.ExpiresAt));
    }

    public async Task<AccessTokenResult?> IssueAccessTokenAsync(string? token, CancellationToken cancellationToken)
    {
        var session = await FindActiveAsync(token, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var jwt = jwtTokenGenerator.Generate(session.User);
        return new AccessTokenResult(jwt.Value, jwt.ExpiresAtUtc);
    }

    private async Task<Session?> FindActiveAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var session = await sessionRepository.GetByTokenHashAsync(Session.HashToken(token), cancellationToken);
        return session is not null && session.IsActive(DateTime.UtcNow) ? session : null;
    }
}
