using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Portal.Api.Configuration.Server;

/// <summary>
/// Applies [proxy] to the standard ASP.NET Core Forwarded Headers Middleware. Only X-Forwarded-For is
/// processed, with the default ForwardLimit of 1 (one proxy hop). The framework's default trust (loopback
/// only) is kept; the configured addresses and networks are added to it. Trust validation is never disabled.
/// </summary>
public static class ForwardedHeadersSetup
{
    public static void Configure(ForwardedHeadersOptions options, ServerSettings settings)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
        options.ForwardLimit = 1;

        // KnownProxies / KnownIPNetworks already contain the loopback defaults: add, never replace or clear.
        foreach (var proxy in settings.TrustedProxies)
        {
            options.KnownProxies.Add(proxy);
        }

        foreach (var network in settings.TrustedNetworks)
        {
            options.KnownIPNetworks.Add(network);
        }
    }
}
