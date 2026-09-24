using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IEventPublisher eventPublisher)
{
    public async Task<RegisterUserResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var user = new User(Guid.NewGuid(), NormalizeEmail(request.Email), passwordHasher.Hash(request.Password), DateTime.UtcNow);

        await userRepository.AddAsync(user, cancellationToken);
        await eventPublisher.PublishUserRegisteredAsync(user, cancellationToken);

        return new RegisterUserResult(user.Id, user.Email);
    }

    public async Task<LoginUserResult> LoginAsync(LoginUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        if (user is null)
        {
            passwordHasher.SimulateVerify(request.Password);
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        var token = jwtTokenGenerator.Generate(user);
        await eventPublisher.PublishUserLoggedInAsync(user, cancellationToken);

        return new LoginUserResult(token.Value, token.ExpiresAtUtc);
    }

    // Emails are stored lowercased so the unique index on users.Email also
    // rejects case variants of an existing address.
    private static string NormalizeEmail(string email) => email.ToLowerInvariant();
}
