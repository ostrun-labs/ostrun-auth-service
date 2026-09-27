using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace OstrunAuthService.IntegrationTests;

// Runs the real API in memory against a throwaway Postgres container. Shared
// by every test in the "api" collection; tests use unique emails instead of
// resetting the database.
public sealed class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Issuer = "ostrun-auth-tests";
    public const string Audience = "ostrun-auth-tests";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var rsa = RSA.Create(2048);

        // Environment variables rather than WebApplicationFactory settings:
        // Program.cs reads configuration before building the host, where
        // factory settings aren't applied yet.
        Environment.SetEnvironmentVariable("ConnectionStrings__Auth", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SigningKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem())));
        Environment.SetEnvironmentVariable("Jwt__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", Audience);
        Environment.SetEnvironmentVariable("RabbitMq__Host", null);
    }

    // The session cookie is Secure, so the client must talk "https" for its
    // cookie container to send it back.
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<AuthApiFactory>
{
    public const string Name = "api";
}
