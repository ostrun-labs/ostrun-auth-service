using System.Text.Json;

namespace OstrunAuthService.Infrastructure.Events;

// Parses the raw "Events:Subscriptions" config value: a single flat string holding a JSON
// array, not a bound config section. Standalone runs typically leave it unset, so an
// empty/absent value is the default case, not an error.
public static class EventSubscriptionsParser
{
    public static IReadOnlyList<EventSubscription> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<EventSubscription>();
        }

        List<RawEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<RawEntry>>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Events:Subscriptions is not valid JSON: {ex.Message}", ex);
        }

        if (entries is null)
        {
            return Array.Empty<EventSubscription>();
        }

        var subscriptions = new List<EventSubscription>(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            if (string.IsNullOrWhiteSpace(entry.Event))
            {
                throw new InvalidOperationException(
                    $"Events:Subscriptions[{i}] is missing a non-empty \"event\".");
            }

            if (string.IsNullOrWhiteSpace(entry.Url) ||
                !Uri.IsWellFormedUriString(entry.Url, UriKind.Absolute))
            {
                throw new InvalidOperationException(
                    $"Events:Subscriptions[{i}] (\"{entry.Event}\") has a missing or non-absolute \"url\".");
            }

            subscriptions.Add(new EventSubscription(entry.Event, new Uri(entry.Url, UriKind.Absolute)));
        }

        return subscriptions;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record RawEntry(string? Event, string? Url);
}
