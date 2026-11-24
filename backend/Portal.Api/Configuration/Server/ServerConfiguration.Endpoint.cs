using Microsoft.Extensions.Configuration;

namespace Portal.Api.Configuration.Server;

public static partial class ServerConfiguration
{
    private static void CheckCertificateFile(string key, string? raw, string? path, string? publicPath, ServerConfigReport report)
    {
        if (raw is null || path is null)
        {
            return;
        }

        if (publicPath is not null && PortalPaths.IsSameOrUnder(path, publicPath))
        {
            report.Errors.Add(
                $"[tls] {key}: {path} is inside [paths] public ({publicPath}); certificate files would be served to browsers. " +
                "Keep certificates in the private conf directory or another directory outside public.");
            return;
        }

        if (Directory.Exists(path))
        {
            report.Errors.Add($"[tls] {key}: {path} is a directory; expected a file.");
            return;
        }

        if (!File.Exists(path))
        {
            report.Errors.Add($"[tls] {key}: file not found: {path} (relative paths are resolved against the {ProductInfo.Name} home).");
            return;
        }

        try
        {
            using var probe = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (UnauthorizedAccessException)
        {
            report.Errors.Add($"[tls] {key}: access denied reading {path}. The {ProductInfo.Name} account needs Read permission.");
        }
        catch (IOException ex)
        {
            report.Errors.Add($"[tls] {key}: cannot read {path} ({ex.GetType().Name}).");
        }
    }

    /// <summary>
    /// [server] wins over ASPNETCORE_URLS / --urls and the *_PORTS settings, which would add listeners.
    /// Values are not printed. A configured Kestrel:Endpoints section would also add listeners: that is an error.
    /// </summary>
    private static void CheckCompetingEndpoints(ConfigurationManager config, ServerConfigReport report)
    {
        if (!report.Settings.ExplicitEndpoint)
        {
            return;
        }

        var competing = new List<string>();
        if (!string.IsNullOrWhiteSpace(config["urls"])) competing.Add("ASPNETCORE_URLS / --urls");
        if (!string.IsNullOrWhiteSpace(config["http_ports"])) competing.Add("ASPNETCORE_HTTP_PORTS");
        if (!string.IsNullOrWhiteSpace(config["https_ports"])) competing.Add("ASPNETCORE_HTTPS_PORTS");

        if (competing.Count > 0)
        {
            var message =
                $"{string.Join(", ", competing)} is set, but [server] configures the listening endpoint and takes precedence; " +
                "the competing setting is ignored.";
            report.Warnings.Add(message);
            report.Settings.EndpointWarnings.Add(message);
        }

        if (config.GetSection("Kestrel:Endpoints").GetChildren().Any())
        {
            report.Errors.Add(
                "Kestrel:Endpoints is configured (appsettings or environment) and would add listeners in addition to " +
                "[server]. Remove it, or remove protocol, http_addr and port from [server] to use it instead.");
        }
    }
}
