using NSubstitute;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Application.Sessions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.UnitTests.Sessions;

public class SessionServiceTests
{
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly SessionService _sut;
    private readonly User _user = User.RegisterWithPassword("user@ostrun.dev", "hashed", DateTime.UtcNow);

    public SessionServiceTests()
    {
        _sut = new SessionService(_sessionRepository, _jwtTokenGenerator);
    }

    [Fact]
    public async Task GetCurrentAsync_WithActiveSession_ReturnsUserAndSession()
    {
        var token = Stored(DateTime.UtcNow);

        var current = await _sut.GetCurrentAsync(token, CancellationToken.None);

        Assert.NotNull(current);
        Assert.Equal(_user.Id, current.User.Id);
        Assert.Equal("user@ostrun.dev", current.User.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-token")]
    public async Task GetCurrentAsync_WithoutKnownSession_ReturnsNull(string? token)
    {
        Assert.Null(await _sut.GetCurrentAsync(token, CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentAsync_WithExpiredSession_ReturnsNull()
    {
        var token = Stored(DateTime.UtcNow - Session.Lifetime - TimeSpan.FromMinutes(1));

        Assert.Null(await _sut.GetCurrentAsync(token, CancellationToken.None));
    }

    [Fact]
    public async Task IssueAccessTokenAsync_WithActiveSession_ReturnsJwtForSessionUser()
    {
        var token = Stored(DateTime.UtcNow);
        _jwtTokenGenerator.Generate(_user).Returns(new JwtToken("signed-jwt", DateTime.UtcNow.AddHours(1)));

        var accessToken = await _sut.IssueAccessTokenAsync(token, CancellationToken.None);

        Assert.Equal("signed-jwt", accessToken?.Token);
    }

    [Fact]
    public async Task IssueAccessTokenAsync_WithExpiredSession_ReturnsNull()
    {
        var token = Stored(DateTime.UtcNow - Session.Lifetime - TimeSpan.FromMinutes(1));

        Assert.Null(await _sut.IssueAccessTokenAsync(token, CancellationToken.None));
        _jwtTokenGenerator.DidNotReceive().Generate(Arg.Any<User>());
    }

    private string Stored(DateTime startedAt)
    {
        var (session, token) = Session.Start(_user, startedAt, null, null);
        _sessionRepository.GetByTokenHashAsync(session.TokenHash, Arg.Any<CancellationToken>()).Returns(session);
        return token;
    }
}
