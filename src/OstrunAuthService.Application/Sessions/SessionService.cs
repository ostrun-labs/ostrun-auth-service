using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Auth;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Sessions;

public sealed class SessionService(
    ISessionRepository sessionRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork)
{
    // The last step of every sign-in method. Commits the session together with
    // whatever the caller staged (e.g. a new user), plus UserLoggedIn.
    public async Task<LoginOutcome> StartAsync(User user, ClientInfo client, CancellationToken cancellationToken)
    {
        var (session, sessionToken) = Session.Start(user, DateTime.UtcNow, client.IpAddress, client.UserAgent);
        sessionRepository.Add(session);
        await eventPublisher.PublishUserLoggedInAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var jwt = jwtTokenGenerator.Generate(user);
        return new LoginOutcome(
            new AccessTokenResult(jwt.Value, jwt.ExpiresAtUtc),
            new IssuedSession(sessionToken, session.ExpiresAt));
    }

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

    // Idempotent: signing out without a valid session is not an error.
    public async Task SignOutAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        var session = await sessionRepository.GetByTokenHashAsync(Session.HashToken(token), cancellationToken);
        if (session is null)
        {
            return;
        }

        sessionRepository.Remove(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
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
