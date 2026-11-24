using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Portal.Api.Configuration.Server;

/// <summary>
/// Stage 4: applies the [server] endpoint to Kestrel. Exactly one listener is created: the configured
/// address and port, with TLS when protocol is https. Nothing else is added, and https never falls back to http.
/// </summary>
public static class KestrelSetup
{
    /// <summary>Human-readable endpoint for diagnostics and the startup log, e.g. "https://127.0.0.1:5247".</summary>
    public static string Describe(ServerSettings settings)
    {
        var address = settings.HttpAddr.Length == 0 ? "[all interfaces]" : settings.HttpAddr;
        return $"{settings.Protocol}://{address}:{settings.Port}";
    }

    /// <summary>
    /// Configures the one listener. For https the certificate must already be loaded; passing none is an
    /// error, so a missing certificate can never turn into an unencrypted listener.
    /// </summary>
    public static void Configure(KestrelServerOptions options, ServerSettings settings, X509Certificate2? certificate)
    {
        var https = settings.Protocol == "https";
        if (https && certificate is null)
        {
            throw new InvalidOperationException("protocol is https but no certificate was loaded; refusing to listen without TLS.");
        }

        void Bind(ListenOptions listen)
        {
            if (https)
            {
                listen.UseHttps(certificate!);
            }
        }

        if (settings.HttpAddr.Length == 0)
        {
            options.ListenAnyIP(settings.Port, Bind);
        }
        else if (string.Equals(settings.HttpAddr, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            options.ListenLocalhost(settings.Port, Bind);
        }
        else
        {
            options.Listen(IPAddress.Parse(settings.HttpAddr), settings.Port, Bind);
        }
    }
}
