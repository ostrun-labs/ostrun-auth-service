using NSubstitute;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Auth;
using OstrunAuthService.Application.Sessions;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.UnitTests.Auth;

public class AuthServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AuthService _sut;

    private static readonly ClientInfo Client = new("203.0.113.7", "test-agent");

    public AuthServiceTests()
    {
        _sut = new AuthService(_userRepository, _passwordHasher, _eventPublisher, _unitOfWork,
            new SessionService(_sessionRepository, _jwtTokenGenerator, _eventPublisher, _unitOfWork));
    }

    [Fact]
    public async Task RegisterAsync_WithNewEmail_CommitsUserAndEventTogether()
    {
        _passwordHasher.Hash("Password123!").Returns("hashed-password");

        var result = await _sut.RegisterAsync(new RegisterUserRequest("new@ostrun.dev", "Password123!"), CancellationToken.None);

        Assert.Equal("new@ostrun.dev", result.Email);
        Received.InOrder(() =>
        {
            _userRepository.Add(Arg.Is<User>(u => u.Email == "new@ostrun.dev" && u.PasswordHash == "hashed-password"));
            _eventPublisher.PublishUserRegisteredAsync(Arg.Is<User>(u => u.Id == result.UserId), Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RegisterAsync_StoresEmailLowercased()
    {
        _passwordHasher.Hash("Password123!").Returns("hashed-password");

        var result = await _sut.RegisterAsync(new RegisterUserRequest("New@Ostrun.DEV", "Password123!"), CancellationToken.None);

        Assert.Equal("new@ostrun.dev", result.Email);
        _userRepository.Received(1).Add(Arg.Is<User>(u => u.Email == "new@ostrun.dev"));
    }

    [Fact]
    public async Task LoginAsync_LooksUpEmailLowercased()
    {
        var user = User.RegisterWithPassword("user@ostrun.dev", "hashed-password", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("hashed-password", "Password123!").Returns(true);
        _jwtTokenGenerator.Generate(user).Returns(new JwtToken("signed-jwt", DateTime.UtcNow.AddHours(1)));

        var result = await _sut.LoginAsync(new LoginUserRequest("USER@Ostrun.dev", "Password123!"), Client, CancellationToken.None);

        Assert.Equal("signed-jwt", result.AccessToken.Token);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ThrowsEmailAlreadyRegistered()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new EmailAlreadyRegisteredException("taken@ostrun.dev")));

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() =>
            _sut.RegisterAsync(new RegisterUserRequest("taken@ostrun.dev", "Password123!"), CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_StartsSessionAndReturnsToken()
    {
        var user = User.RegisterWithPassword("user@ostrun.dev", "hashed-password", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("hashed-password", "Password123!").Returns(true);
        _jwtTokenGenerator.Generate(user).Returns(new JwtToken("signed-jwt", DateTime.UtcNow.AddHours(1)));

        var result = await _sut.LoginAsync(new LoginUserRequest("user@ostrun.dev", "Password123!"), Client, CancellationToken.None);

        Assert.Equal("signed-jwt", result.AccessToken.Token);
        var expectedHash = Session.HashToken(result.Session.Token);
        Received.InOrder(() =>
        {
            _sessionRepository.Add(Arg.Is<Session>(s =>
                s.UserId == user.Id && s.TokenHash == expectedHash && s.IpAddress == "203.0.113.7" && s.UserAgent == "test-agent"));
            _eventPublisher.PublishUserLoggedInAsync(user, Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        Assert.NotEqual(expectedHash, result.Session.Token);
        Assert.InRange(result.Session.ExpiresAtUtc, DateTime.UtcNow.AddDays(7).AddMinutes(-1), DateTime.UtcNow.AddDays(7));
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsInvalidCredentials()
    {
        _userRepository.GetByEmailAsync("missing@ostrun.dev", Arg.Any<CancellationToken>()).Returns((User?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginUserRequest("missing@ostrun.dev", "Password123!"), Client, CancellationToken.None));

        _passwordHasher.Received(1).SimulateVerify("Password123!");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsInvalidCredentials()
    {
        var user = User.RegisterWithPassword("user@ostrun.dev", "hashed-password", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("hashed-password", "wrong-password").Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginUserRequest("user@ostrun.dev", "wrong-password"), Client, CancellationToken.None));
    }
}
