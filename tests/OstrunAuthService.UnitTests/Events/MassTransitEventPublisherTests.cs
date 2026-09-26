using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using OstrunAuthService.Domain.Entities;
using OstrunAuthService.Infrastructure.Events;

namespace OstrunAuthService.UnitTests.Events;

public class MassTransitEventPublisherTests
{
    [Fact]
    public async Task PublishUserRegisteredAsync_PublishesMessageWithMatchingFields()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        using var scope = provider.CreateScope();
        var sut = new MassTransitEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>());
        var user = User.RegisterWithPassword("new@ostrun.dev", "hashed", DateTime.UtcNow);

        await sut.PublishUserRegisteredAsync(user, CancellationToken.None);

        Assert.True(await harness.Published.Any<UserRegistered>());
        var published = harness.Published.Select<UserRegistered>().First().Context!.Message;
        Assert.Equal(user.Id, published.UserId);
        Assert.Equal(user.Email, published.Email);
        Assert.Equal(user.CreatedAt, published.RegisteredAt);
    }

    [Fact]
    public async Task PublishUserLoggedInAsync_PublishesMessageWithMatchingFields()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        using var scope = provider.CreateScope();
        var sut = new MassTransitEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>());
        var user = User.RegisterWithPassword("user@ostrun.dev", "hashed", DateTime.UtcNow);

        await sut.PublishUserLoggedInAsync(user, CancellationToken.None);

        Assert.True(await harness.Published.Any<UserLoggedIn>());
        var published = harness.Published.Select<UserLoggedIn>().First().Context!.Message;
        Assert.Equal(user.Id, published.UserId);
        Assert.Equal(user.Email, published.Email);
    }
}
