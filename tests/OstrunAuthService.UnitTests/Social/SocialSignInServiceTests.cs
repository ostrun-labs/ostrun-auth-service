using NSubstitute;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Auth;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Application.Sessions;
using OstrunAuthService.Application.Social;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.UnitTests.Social;

public class SocialSignInServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SocialSignInService _sut;

    private static readonly ClientInfo Client = new("203.0.113.7", "test-agent");

    public SocialSignInServiceTests()
    {
        _jwtTokenGenerator.Generate(Arg.Any<User>()).Returns(new JwtToken("signed-jwt", DateTime.UtcNow.AddHours(1)));
        _sut = new SocialSignInService(_userRepository, _eventPublisher,
            new SessionService(_sessionRepository, _jwtTokenGenerator, _eventPublisher, _unitOfWork));
    }

    [Fact]
    public async Task SignInAsync_WithKnownAccount_StartsSessionForItsUser()
    {
        var user = User.RegisterWithSocialAccount("user@ostrun.dev", null, null, "google", "sub-1", DateTime.UtcNow);
        _userRepository.GetByAccountAsync("google", "sub-1", Arg.Any<CancellationToken>()).Returns(user);

        var outcome = await _sut.SignInAsync(Identity(email: "changed@ostrun.dev"), Client, CancellationToken.None);

        Assert.Equal("signed-jwt", outcome.AccessToken.Token);
        _sessionRepository.Received(1).Add(Arg.Is<Session>(s => s.UserId == user.Id));
        _userRepository.DidNotReceive().Add(Arg.Any<User>());
        await _eventPublisher.DidNotReceive().PublishUserRegisteredAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SignInAsync_WithNewIdentity_RegistersVerifiedUserAndCommitsEverythingTogether()
    {
        var outcome = await _sut.SignInAsync(Identity(email: "New@Ostrun.dev", name: "Ada", image: "https://img/ada"), Client, CancellationToken.None);

        Received.InOrder(() =>
        {
            _userRepository.Add(Arg.Is<User>(u =>
                u.Email == "new@ostrun.dev" && u.EmailVerified && u.Name == "Ada" && u.Image == "https://img/ada" &&
                u.PasswordHash == null &&
                u.Accounts.Single().ProviderId == "google" && u.Accounts.Single().ProviderAccountId == "sub-1"));
            _eventPublisher.PublishUserRegisteredAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
            _sessionRepository.Add(Arg.Any<Session>());
            _eventPublisher.PublishUserLoggedInAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        Assert.NotNull(outcome.Session.Token);
    }

    [Fact]
    public async Task SignInAsync_WithVerifiedEmailOfExistingUser_LinksTheAccountToThatUser()
    {
        var existing = User.RegisterWithPassword("user@ostrun.dev", "hashed", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(existing);

        await _sut.SignInAsync(Identity(email: "USER@ostrun.dev"), Client, CancellationToken.None);

        Assert.Equal(["credential", "google"], existing.Accounts.Select(a => a.ProviderId).Order());
        Assert.True(existing.EmailVerified);
        Assert.Equal("hashed", existing.PasswordHash);
        _userRepository.DidNotReceive().Add(Arg.Any<User>());
        _sessionRepository.Received(1).Add(Arg.Is<Session>(s => s.UserId == existing.Id));
    }

    [Theory]
    [InlineData("user@ostrun.dev", false)]
    [InlineData(null, true)]
    public async Task SignInAsync_WithNewIdentityWithoutVerifiedEmail_IsRefused(string? email, bool emailVerified)
    {
        var existing = User.RegisterWithPassword("user@ostrun.dev", "hashed", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(existing);

        await Assert.ThrowsAsync<SocialEmailNotVerifiedException>(() =>
            _sut.SignInAsync(Identity(email: email, emailVerified: emailVerified), Client, CancellationToken.None));

        Assert.Single(existing.Accounts);
        _sessionRepository.DidNotReceive().Add(Arg.Any<Session>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static ExternalIdentity Identity(string? email, bool emailVerified = true, string? name = null, string? image = null) =>
        new("google", "sub-1", email, emailVerified, name, image);
}
