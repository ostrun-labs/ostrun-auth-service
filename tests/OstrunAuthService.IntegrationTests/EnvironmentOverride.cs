namespace OstrunAuthService.IntegrationTests;

// Sets environment variables for a second app instance and restores them on
// dispose. Safe inside the "api" collection: its tests run one at a time,
// and the shared app has already read its configuration.
public sealed class EnvironmentOverride : IDisposable
{
    private readonly Dictionary<string, string?> _originals = [];

    public EnvironmentOverride(params (string Name, string? Value)[] variables)
    {
        foreach (var (name, value) in variables)
        {
            _originals[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _originals)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
