using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OstrunAuthService.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ProvidersTests(AuthApiFactory factory)
{
    [Fact]
    public async Task Providers_WithoutSocialConfig_ListsOnlyPasswordSignIn()
    {
        var body = await factory.CreateHttpsClient().GetFromJsonAsync<ProvidersResponse>("/auth/providers");

        Assert.Equal(["credential"], body!.Providers);
    }

    [Fact]
    public async Task Providers_WithGoogleConfigured_ListsGoogle()
    {
        using var _ = new EnvironmentOverride(("Google__ClientId", "test-client-id"), ("Google__ClientSecret", "test-client-secret"));
        using var app = new WebApplicationFactory<Program>();

        var body = await app.CreateClient().GetFromJsonAsync<ProvidersResponse>("/auth/providers");

        Assert.Equal(["credential", "google"], body!.Providers);
    }

    [Theory]
    [InlineData("Google__ClientId")]
    [InlineData("Google__ClientSecret")]
    public void Startup_WithHalfTheGoogleConfig_FailsWithAClearError(string onlySetting)
    {
        using var _ = new EnvironmentOverride((onlySetting, "only-this-one"));
        using var app = new WebApplicationFactory<Program>();

        var error = Record.Exception(() => app.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("Set both Google__ClientId and Google__ClientSecret", error.ToString());
    }

    private sealed record ProvidersResponse(string[] Providers);
}
