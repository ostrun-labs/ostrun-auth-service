using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OstrunAuthService.Infrastructure.Persistence;

// Used by `dotnet ef migrations` at design time, where the Api's DI container
// (and its required Jwt config) isn't available. The connection string here
// only needs to be valid enough for Npgsql to generate SQL; it's never
// actually opened by the CLI tooling for `migrations add`.
public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=ostrun_auth;Username=postgres;Password=postgres");

        return new AuthDbContext(optionsBuilder.Options);
    }
}
