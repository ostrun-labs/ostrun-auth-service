using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Application.Sessions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork,
    SessionService sessionService)
{
    public async Task<RegisterUserResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var user = User.RegisterWithPassword(User.NormalizeEmail(request.Email), passwordHasher.Hash(request.Password), DateTime.UtcNow);

        userRepository.Add(user);
        await eventPublisher.PublishUserRegisteredAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterUserResult(user.Id, user.Email);
    }

    public async Task<LoginOutcome> LoginAsync(LoginUserRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);
        if (user?.PasswordHash is null)
        {
            passwordHasher.SimulateVerify(request.Password);
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        return await sessionService.StartAsync(user, client, cancellationToken);
    }
}
