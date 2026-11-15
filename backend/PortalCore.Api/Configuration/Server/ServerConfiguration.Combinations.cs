namespace PortalCore.Api.Configuration.Server;

public static partial class ServerConfiguration
{
    /// <summary>Checks that combine several settings.</summary>
    private static void CheckCombinations(ServerConfigReport report)
    {
        var s = report.Settings;

        // Private and backend directories must never be reachable through the public web root.
        if (s.PublicPath is not null)
        {
            var privateLocations = new List<(string Description, string? Path)>
            {
                ("the private conf directory (server.ini)", report.Home.Path is null ? null : Path.Combine(report.Home.Path, ConfigFolder)),
                ("[paths] data (the database)", s.DataPath),
                ("[paths] logs", s.LogsPath),
                ("[paths] ai", s.AiPath),
                ("[paths] backups", s.BackupsPath),
                // Stage 5: the application directory holds appsettings.json and the binaries.
                ("the application directory (content root)", report.ContentRoot),
            };

            foreach (var (description, path) in privateLocations)
            {
                if (path is not null && IsUnderOrEqual(path, s.PublicPath))
                {
                    report.Errors.Add(
                        $"[paths] public: resolves to {s.PublicPath}, which contains {description} at {path}. " +
                        "Private files would be served to browsers; choose separate directories.");
                }
            }
        }

        // Backups must never sit in, or contain, the private configuration directory.
        if (s.BackupsPath is not null && report.Home.Path is not null &&
            PortalPaths.Overlaps(s.BackupsPath, Path.Combine(report.Home.Path, ConfigFolder)))
        {
            report.Errors.Add(
                $"[paths] backups: resolves to {s.BackupsPath}, which overlaps the private conf directory. " +
                "Backups must be kept separate from the configuration.");
        }

        if (s.RootUrl is not null && !string.Equals(s.RootUrl.Scheme, s.Protocol, StringComparison.OrdinalIgnoreCase))
        {
            report.Notices.Add(
                $"[server] root_url uses {s.RootUrl.Scheme} while protocol is {s.Protocol}; " +
                "expected only when a reverse proxy or gateway sits in front of Portal Core.");
        }

        if (s.Protocol == "http" && IsNonLoopbackListener(s.HttpAddr))
        {
            report.Warnings.Add(
                "[server] http_addr accepts connections from other computers while protocol is http (unencrypted).");
        }

        if (!s.LogConsole && !s.LogFile)
        {
            report.Warnings.Add("[log] console and file are both false; no diagnostic log output would be produced.");
        }
    }
}
