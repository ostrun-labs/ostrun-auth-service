namespace OstrunAuthService.Infrastructure.Events;

public sealed record EventSubscription(string Event, Uri Url);
