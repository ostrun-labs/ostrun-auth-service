using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OstrunAuthService.Domain.Entities;
using OstrunAuthService.Infrastructure.Events;

namespace OstrunAuthService.UnitTests.Events;

public class HttpEventPublisherTests
{
    private readonly ILogger<HttpEventPublisher> _logger = Substitute.For<ILogger<HttpEventPublisher>>();

    [Fact]
    public async Task PublishUserRegisteredAsync_WithMatchingSubscriber_PostsCorrectPayload()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK);
        var sut = CreateSut(handler, new EventSubscription("Ostrun.Auth.UserRegistered", new Uri("http://mailer-api:8080/events/registered")));
        var user = new User(Guid.NewGuid(), "new@ostrun.dev", "hashed", DateTime.UtcNow);

        await sut.PublishUserRegisteredAsync(user, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://mailer-api:8080/events/registered", request.RequestUri!.ToString());
        var body = JsonDocument.Parse(request.Body!).RootElement;
        Assert.Equal(user.Id.ToString(), body.GetProperty("userId").GetString());
        Assert.Equal(user.Email, body.GetProperty("email").GetString());
        Assert.True(body.TryGetProperty("registeredAt", out _));
    }

    [Fact]
    public async Task PublishUserRegisteredAsync_WithSubscriberForDifferentEvent_DoesNotCallIt()
    {
        var handler = new FakeHttpMessageHandler();
        var sut = CreateSut(handler, new EventSubscription("Ostrun.Auth.UserLoggedIn", new Uri("http://mailer-api:8080/events/logged-in")));
        var user = new User(Guid.NewGuid(), "new@ostrun.dev", "hashed", DateTime.UtcNow);

        await sut.PublishUserRegisteredAsync(user, CancellationToken.None);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PublishUserLoggedInAsync_WithSubscriberFailingOnce_DeliversOnRetry()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("boom"));
        handler.Enqueue(HttpStatusCode.OK);
        var sut = CreateSut(handler, new EventSubscription("Ostrun.Auth.UserLoggedIn", new Uri("http://mailer-api:8080/events/logged-in")));
        var user = new User(Guid.NewGuid(), "user@ostrun.dev", "hashed", DateTime.UtcNow);

        var exception = await Record.ExceptionAsync(() => sut.PublishUserLoggedInAsync(user, CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task PublishUserLoggedInAsync_WithSubscriberFailingEveryAttempt_DoesNotThrow()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("boom"));
        handler.EnqueueException(new HttpRequestException("boom again"));
        var sut = CreateSut(handler, new EventSubscription("Ostrun.Auth.UserLoggedIn", new Uri("http://mailer-api:8080/events/logged-in")));
        var user = new User(Guid.NewGuid(), "user@ostrun.dev", "hashed", DateTime.UtcNow);

        var exception = await Record.ExceptionAsync(() => sut.PublishUserLoggedInAsync(user, CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task PublishUserRegisteredAsync_WithNoSubscriptions_IsNoOp()
    {
        var handler = new FakeHttpMessageHandler();
        var sut = CreateSut(handler);
        var user = new User(Guid.NewGuid(), "new@ostrun.dev", "hashed", DateTime.UtcNow);

        var exception = await Record.ExceptionAsync(() => sut.PublishUserRegisteredAsync(user, CancellationToken.None));

        Assert.Null(exception);
        Assert.Empty(handler.Requests);
    }

    private HttpEventPublisher CreateSut(FakeHttpMessageHandler handler, params EventSubscription[] subscriptions)
    {
        var client = new HttpClient(handler);
        var factory = new SingleClientHttpClientFactory(client);

        return new HttpEventPublisher(factory, subscriptions, _logger);
    }

    private sealed class SingleClientHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri? RequestUri, string? Body);

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _behaviors = new();

        public List<CapturedRequest> Requests { get; } = new();

        public void Enqueue(HttpStatusCode statusCode) =>
            _behaviors.Enqueue(() => new HttpResponseMessage(statusCode));

        public void EnqueueException(Exception exception) =>
            _behaviors.Enqueue(() => throw exception);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri, body));

            if (_behaviors.Count == 0)
            {
                throw new InvalidOperationException("FakeHttpMessageHandler received an unscripted call.");
            }

            return _behaviors.Dequeue()();
        }
    }
}
