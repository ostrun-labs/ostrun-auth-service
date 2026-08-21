using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OstrunAuthService.Application.Abstractions;
using OstrunAuthService.Domain.Entities;

namespace OstrunAuthService.Infrastructure.Events;

// Manual Auth -> Mailer HTTP spike: no message broker chosen yet, so this posts each
// event straight to the subscriber URLs configured in Events:Subscriptions. Best-effort
// only (one retry, then drop) — replace with an outbox + broker client once that
// decision is made, without touching Application or Api.
public sealed class HttpEventPublisher(
    IHttpClientFactory httpClientFactory,
    IReadOnlyList<EventSubscription> subscriptions,
    ILogger<HttpEventPublisher> logger) : IEventPublisher
{
    public const string HttpClientName = "event-publisher";

    private const int MaxAttempts = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public Task PublishUserRegisteredAsync(User user, CancellationToken cancellationToken)
    {
        var payload = new
        {
            userId = user.Id,
            email = user.Email,
            registeredAt = user.CreatedAt,
        };

        return PublishAsync("Ostrun.Auth.UserRegistered", payload, cancellationToken);
    }

    public Task PublishUserLoggedInAsync(User user, CancellationToken cancellationToken)
    {
        var payload = new
        {
            userId = user.Id,
            email = user.Email,
            loggedInAt = DateTime.UtcNow,
        };

        return PublishAsync("Ostrun.Auth.UserLoggedIn", payload, cancellationToken);
    }

    private async Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        var matches = subscriptions.Where(s => s.Event == eventName).ToList();

        foreach (var subscription in matches)
        {
            await DeliverAsync(eventName, subscription.Url, payload, cancellationToken);
        }
    }

    private async Task DeliverAsync(string eventName, Uri url, object payload, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var json = JsonSerializer.Serialize(payload, PayloadJsonOptions);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var isLastAttempt = attempt == MaxAttempts;

            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Delivered {Event} to {Url}", eventName, url);
                    return;
                }

                if (isLastAttempt)
                {
                    logger.LogWarning("Dropped {Event} to {Url} after {Attempts} attempts", eventName, url, MaxAttempts);
                    return;
                }
            }
            catch (HttpRequestException) when (!isLastAttempt)
            {
            }
            catch (HttpRequestException)
            {
                logger.LogWarning("Dropped {Event} to {Url} after {Attempts} attempts", eventName, url, MaxAttempts);
                return;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && !isLastAttempt)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Dropped {Event} to {Url} after {Attempts} attempts", eventName, url, MaxAttempts);
                return;
            }

            await Task.Delay(RetryDelay, cancellationToken);
        }
    }
}
