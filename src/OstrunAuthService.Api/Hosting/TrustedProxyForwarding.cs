using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace OstrunAuthService.Api.Hosting;

// Behind a TLS-terminating gateway the service sees http://<internal host>.
// X-Forwarded-Proto/Host/For restore the public URL (for OAuth redirect
// URIs) and the client IP (for sessions), but only from proxies listed in
// Auth__TrustedProxies; anyone else could forge them.
public static class TrustedProxyForwarding
{
    private const string SettingName = "Auth:TrustedProxies";

    public static IServiceCollection AddTrustedProxyForwarding(this IServiceCollection services, IConfiguration configuration)
    {
        var entries = (configuration[SettingName] ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (entries.Length == 0)
        {
            return services;
        }

        var proxies = new List<IPAddress>();
        var networks = new List<IPNetwork>();
        foreach (var entry in entries)
        {
            if (!TryParse(entry, proxies, networks))
            {
                throw new FormatException($"Auth__TrustedProxies entry '{entry}' must be an IP address or a CIDR range such as 172.16.0.0/12.");
            }
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            // Empty lists would mean "trust every proxy", so replace the
            // loopback defaults with exactly the configured ones.
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
            proxies.ForEach(options.KnownProxies.Add);
            networks.ForEach(options.KnownNetworks.Add);
        });
        services.AddSingleton<TrustedProxiesMarker>();
        return services;
    }

    public static IApplicationBuilder UseTrustedProxyForwarding(this IApplicationBuilder app)
    {
        if (app.ApplicationServices.GetService<TrustedProxiesMarker>() is not null)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }

    private static bool TryParse(string entry, List<IPAddress> proxies, List<IPNetwork> networks)
    {
        var parts = entry.Split('/');
        if (!IPAddress.TryParse(parts[0], out var address))
        {
            return false;
        }

        if (parts.Length == 1)
        {
            proxies.Add(address);
            return true;
        }

        var maxPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (parts.Length != 2 || !int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > maxPrefix)
        {
            return false;
        }

        networks.Add(new IPNetwork(address, prefix));
        return true;
    }

    private sealed class TrustedProxiesMarker;
}
