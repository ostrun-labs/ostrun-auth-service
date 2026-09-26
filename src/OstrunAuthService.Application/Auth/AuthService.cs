using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork)
{
    public async Task<RegisterUserResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var user = User.RegisterWithPassword(NormalizeEmail(request.Email), passwordHasher.Hash(request.Password), DateTime.UtcNow);

        userRepository.Add(user);
        await eventPublisher.PublishUserRegisteredAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterUserResult(user.Id, user.Email);
    }

    public async Task<LoginOutcome> LoginAsync(LoginUserRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        if (user?.PasswordHash is null)
        {
            passwordHasher.SimulateVerify(request.Password);
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        var (session, sessionToken) = Session.Start(user, DateTime.UtcNow, client.IpAddress, client.UserAgent);
        sessionRepository.Add(session);
        await eventPublisher.PublishUserLoggedInAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var jwt = jwtTokenGenerator.Generate(user);
        return new LoginOutcome(
            new AccessTokenResult(jwt.Value, jwt.ExpiresAtUtc),
            new IssuedSession(sessionToken, session.ExpiresAt));
    }

    // Emails are stored lowercased so the unique index on users.Email also
    // rejects case variants of an existing address.
    private static string NormalizeEmail(string email) => email.ToLowerInvariant();
}
