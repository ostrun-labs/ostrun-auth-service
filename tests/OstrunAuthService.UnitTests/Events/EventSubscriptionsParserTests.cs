using OstrunAuthService.Infrastructure.Events;

namespace OstrunAuthService.UnitTests.Events;

public class EventSubscriptionsParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithNullOrEmptyRaw_ReturnsEmptyList(string? raw)
    {
        var result = EventSubscriptionsParser.Parse(raw);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WithSingleValidEntry_ReturnsCorrectEventAndUrl()
    {
        var raw = """[{"event": "Ostrun.Auth.UserRegistered", "url": "http://mailer-api:8080/events/ostrun.auth.user-registered"}]""";

        var result = EventSubscriptionsParser.Parse(raw);

        var subscription = Assert.Single(result);
        Assert.Equal("Ostrun.Auth.UserRegistered", subscription.Event);
        Assert.Equal("http://mailer-api:8080/events/ostrun.auth.user-registered", subscription.Url.ToString());
    }

    [Fact]
    public void Parse_WithMultipleValidEntries_ReturnsAllParsed()
    {
        var raw = """
            [
                {"event": "Ostrun.Auth.UserRegistered", "url": "http://mailer-api:8080/events/registered"},
                {"event": "Ostrun.Auth.UserLoggedIn", "url": "http://mailer-api:8080/events/logged-in"}
            ]
            """;

        var result = EventSubscriptionsParser.Parse(raw);

        Assert.Equal(2, result.Count);
        Assert.Equal("Ostrun.Auth.UserRegistered", result[0].Event);
        Assert.Equal("Ostrun.Auth.UserLoggedIn", result[1].Event);
    }

    [Fact]
    public void Parse_WithMalformedJson_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => EventSubscriptionsParser.Parse("not-json"));
    }

    [Fact]
    public void Parse_WithEntryMissingEvent_ThrowsInvalidOperationException()
    {
        var raw = """[{"url": "http://mailer-api:8080/events/registered"}]""";

        Assert.Throws<InvalidOperationException>(() => EventSubscriptionsParser.Parse(raw));
    }

    [Fact]
    public void Parse_WithEntryMissingUrl_ThrowsInvalidOperationException()
    {
        var raw = """[{"event": "Ostrun.Auth.UserRegistered"}]""";

        Assert.Throws<InvalidOperationException>(() => EventSubscriptionsParser.Parse(raw));
    }

    [Fact]
    public void Parse_WithEntryWithRelativeUrl_ThrowsInvalidOperationException()
    {
        var raw = """[{"event": "Ostrun.Auth.UserRegistered", "url": "/events/registered"}]""";

        Assert.Throws<InvalidOperationException>(() => EventSubscriptionsParser.Parse(raw));
    }
}
