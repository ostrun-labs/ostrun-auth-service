using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Auth;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Application.Sessions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Application.Social;

public sealed class SocialSignInService(
    IUserRepository userRepository,
    IEventPublisher eventPublisher,
    SessionService sessionService)
{
    /// <exception cref="SocialEmailNotVerifiedException">
    /// The identity is new and its provider didn't verify an email for it.
    /// </exception>
    public async Task<LoginOutcome> SignInAsync(ExternalIdentity identity, ClientInfo client, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByAccountAsync(identity.ProviderId, identity.ProviderAccountId, cancellationToken)
            ?? await LinkOrRegisterAsync(identity, cancellationToken);

        return await sessionService.StartAsync(user, client, cancellationToken);
    }

    private async Task<User> LinkOrRegisterAsync(ExternalIdentity identity, CancellationToken cancellationToken)
    {
        // Matching on an unverified email would let anyone who types a
        // victim's address at the provider take over the victim's account.
        if (identity.Email is null || !identity.EmailVerified)
        {
            throw new SocialEmailNotVerifiedException(identity.ProviderId);
        }

        var email = User.NormalizeEmail(identity.Email);
        var now = DateTime.UtcNow;

        var existing = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            existing.LinkSocialAccount(identity.ProviderId, identity.ProviderAccountId, now);
            return existing;
        }

        var user = User.RegisterWithSocialAccount(email, identity.Name, identity.Image, identity.ProviderId, identity.ProviderAccountId, now);
        userRepository.Add(user);
        await eventPublisher.PublishUserRegisteredAsync(user, cancellationToken);
        return user;
    }
}
