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
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_userRepository, _passwordHasher, _jwtTokenGenerator, _eventPublisher, _unitOfWork);
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

        var result = await _sut.LoginAsync(new LoginUserRequest("USER@Ostrun.dev", "Password123!"), CancellationToken.None);

        Assert.Equal("signed-jwt", result.Token);
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
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        var user = User.RegisterWithPassword("user@ostrun.dev", "hashed-password", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("hashed-password", "Password123!").Returns(true);
        _jwtTokenGenerator.Generate(user).Returns(new JwtToken("signed-jwt", DateTime.UtcNow.AddHours(1)));

        var result = await _sut.LoginAsync(new LoginUserRequest("user@ostrun.dev", "Password123!"), CancellationToken.None);

        Assert.Equal("signed-jwt", result.Token);
        Received.InOrder(() =>
        {
            _eventPublisher.PublishUserLoggedInAsync(user, Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
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
        var user = User.RegisterWithPassword("user@ostrun.dev", "hashed-password", DateTime.UtcNow);
        _userRepository.GetByEmailAsync("user@ostrun.dev", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("hashed-password", "wrong-password").Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.LoginAsync(new LoginUserRequest("user@ostrun.dev", "wrong-password"), CancellationToken.None));
    }
}
