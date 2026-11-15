using Microsoft.Extensions.Configuration;

namespace PortalCore.Api.Configuration.Server;

/// <summary>
/// Stage 4: validation of [tls] and of the listening endpoint. Validation looks at settings and file
/// system entries only. It never loads a certificate and never starts Kestrel; loading is done by
/// <see cref="TlsCertificateLoader"/> at startup.
/// </summary>
public static partial class ServerConfiguration
{
    private static readonly string[] ExplicitEndpointKeys = { "server:protocol", "server:http_addr", "server:port" };
    private static readonly string[] PemLikeExtensions = { ".pem", ".crt", ".cer", ".key" };

    /// <summary>Validates a [tls] value and returns a certificate file resolved against the home directory.</summary>
    private static string? ValidateTls(ServerSchema.Setting setting, string raw, ServerConfigReport report, Action<string> fail)
    {
        var s = report.Settings;

        // The password is kept exactly as given (no trimming) and never goes into a message or a view.
        if (setting.Key == "cert_password")
        {
            s.CertPassword = raw.Length == 0 ? null : raw;
            return null;
        }

        // An empty value means "not set"; protocol = https then reports what is missing.
        if (raw.Length == 0)
        {
            return null;
        }

        var resolved = CheckPath(setting, raw, report.Home.Path, fail);

        if (setting.Key == "cert_file")
        {
            s.CertFile = raw;
            s.CertFilePath = resolved;
        }
        else
        {
            s.CertKeyFile = raw;
            s.CertKeyFilePath = resolved;
        }

        return resolved;
    }

    /// <summary>
    /// True when the endpoint is configured by [server] (any source). Only then does Portal Core
    /// configure Kestrel itself; otherwise ASPNETCORE_URLS / launchSettings.json behave as before.
    /// </summary>
    private static bool IsEndpointExplicit(ConfigurationManager config) =>
        ExplicitEndpointKeys.Any(key => config[key] is not null);

    /// <summary>Settings and files needed for https, and what is ignored for http. Nothing is loaded.</summary>
    private static void CheckTls(ServerConfigReport report)
    {
        var s = report.Settings;
        var anyTls = s.CertFile is not null || s.CertKeyFile is not null || s.CertPassword is not null;

        if (s.Protocol != "https")
        {
            if (anyTls)
            {
                report.Warnings.Add("[tls] settings are present but [server] protocol is http, so they are ignored.");
            }
            return;
        }

        if (s.CertFile is null)
        {
            report.Errors.Add(s.CertKeyFile is null
                ? "[tls] cert_file: required when [server] protocol is https (a PFX file, or a PEM certificate together with cert_key_file)."
                : "[tls] cert_key_file: set without cert_file; give the PEM certificate in cert_file.");
        }

        CheckCertificateFile("cert_file", s.CertFile, s.CertFilePath, s.PublicPath, report);
        CheckCertificateFile("cert_key_file", s.CertKeyFile, s.CertKeyFilePath, s.PublicPath, report);

        if (s.CertFile is not null && s.CertKeyFile is null &&
            PemLikeExtensions.Contains(Path.GetExtension(s.CertFile), StringComparer.OrdinalIgnoreCase))
        {
            report.Warnings.Add(
                "[tls] cert_file looks like a PEM file but cert_key_file is not set; without it the file is read as PFX (PKCS#12).");
        }
    }
}
