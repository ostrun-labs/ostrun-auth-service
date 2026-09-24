using NSubstitute;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Auth;
using OstrunAuthService.Application.Exceptions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.UnitTests.Auth;

public class AuthServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_userRepository, _passwordHasher, _jwtTokenGenerator, _eventPublisher);
    }

    [Fact]
    public async Task RegisterAsync_WithNewEmail_CreatesUserAndPublishesEvent()
    {
        _userRepository.ExistsByEmailAsync("new@ostrun.dev", Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash("Password123!").Returns("hashed-password");

        var result = await _sut.RegisterAsync(new RegisterUserRequest("new@ostrun.dev", "Password123!"), CancellationToken.None);

        Assert.Equal("new@ostrun.dev", result.Email);
        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == "new@ostrun.dev" && u.PasswordHash == "hashed-password"),
            Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishUserRegisteredAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_StoresEmailLowercased()
    {
        _passwordHasher.Hash("Password123!").Returns("hashed-password");

        var result = await _sut.RegisterAsync(new RegisterUserRequest("New@Ostrun.DEV", "Password123!"), CancellationToken.None);

        Assert.Equal("new@ostrun.dev", result.Email);
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u => u.Email == "new@ostrun.dev"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_LooksUpEmailLowercased()
    {
        var user = new User(Guid.NewGuid(), "user@ostrun.dev", "hashed-password", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("hashed-password", "Password123!").Returns(true);
        _jwtTokenGenerator.Generate(user).Returns(new JwtToken("signed-jwt", DateTime.UtcNow.AddHours(1)));

        var result = await _sut.LoginAsync(new LoginUserRequest("USER@Ostrun.dev", "Password123!"), CancellationToken.None);

        Assert.Equal("signed-jwt", result.Token);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ThrowsEmailAlreadyRegistered()
    {
        _userRepository.ExistsByEmailAsync("taken@ostrun.dev", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() =>
            _sut.RegisterAsync(new RegisterUserRequest("taken@ostrun.dev", "Password123!"), CancellationToken.None));

        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        var user = new User(Guid.NewGuid(), "user@ostrun.dev", "hashed-password", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("hashed-password", "Password123!").Returns(true);
        _jwtTokenGenerator.Generate(user).Returns(new JwtToken("signed-jwt", DateTime.UtcNow.AddHours(1)));

        var result = await _sut.LoginAsync(new LoginUserRequest("user@ostrun.dev", "Password123!"), CancellationToken.None);

        Assert.Equal("signed-jwt", result.Token);
        await _eventPublisher.Received(1).PublishUserLoggedInAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsInvalidCredentials()
    {
        _userRepository.GetByEmailAsync("missing@ostrun.dev", Arg.Any<CancellationToken>()).Returns((User?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginUserRequest("missing@ostrun.dev", "Password123!"), CancellationToken.None));

        _passwordHasher.Received(1).SimulateVerify("Password123!");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsInvalidCredentials()
    {
        var user = new User(Guid.NewGuid(), "user@ostrun.dev", "hashed-password", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("hashed-password", "wrong-password").Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginUserRequest("user@ostrun.dev", "wrong-password"), CancellationToken.None));
    }
}
