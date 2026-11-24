using System.Globalization;

namespace Portal.Api.Configuration.Server;

public static partial class ServerConfiguration
{
    private static readonly string[] SupportedProtocols = { "http", "https" };
    private static readonly string[] SupportedLevels = { "debug", "info", "warning", "error" };

    /// <summary>Validates a [server] value. Always returns null (no resolved path).</summary>
    private static string? ValidateServer(ServerSchema.Setting setting, string raw, ServerSettings settings, Action<string> fail)
    {
        switch (setting.Key)
        {
            case "protocol":
                if (SupportedProtocols.Contains(raw, StringComparer.OrdinalIgnoreCase))
                    settings.Protocol = raw.ToLowerInvariant();
                else
                    fail($"unsupported value {Quote(setting, raw)}; supported values: http, https.{CommentHint(raw)}");
                break;

            case "http_addr":
                if (raw.Length == 0 || IsValidListenAddress(raw))
                    settings.HttpAddr = raw;
                else
                    fail($"{Quote(setting, raw)} is not a valid listening address; use an IP address such as " +
                         $"127.0.0.1 or 0.0.0.0, \"localhost\", or leave empty for all interfaces.{CommentHint(raw)}");
                break;

            case "domain":
                if (IsValidHostName(raw))
                    settings.Domain = raw;
                else
                    fail($"{Quote(setting, raw)} is not a valid host name; give a bare name such as " +
                         $"navigationharbour.example.com (no scheme, port or path).{CommentHint(raw)}");
                break;

            case "port":
                if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= 65535)
                    settings.Port = port;
                else
                    fail($"{Quote(setting, raw)} is not a valid port; use a whole number from 1 to 65535.{CommentHint(raw)}");
                break;

            case "root_url":
                if (raw.Length > 0)
                {
                    var problem = CheckRootUrl(raw, out var rootUrl);
                    if (problem is null) settings.RootUrl = rootUrl; else fail(problem);
                }
                break;
        }

        return null;
    }

    /// <summary>Validates a [log] value. Always returns null (no resolved path).</summary>
    private static string? ValidateLog(ServerSchema.Setting setting, string raw, ServerSettings settings, Action<string> fail)
    {
        switch (setting.Key)
        {
            case "level":
                if (SupportedLevels.Contains(raw, StringComparer.OrdinalIgnoreCase))
                    settings.LogLevel = raw.ToLowerInvariant();
                else
                    fail($"unsupported value {Quote(setting, raw)}; supported values: debug, info, warning, error.{CommentHint(raw)}");
                break;

            case "console":
            case "file":
                if (bool.TryParse(raw, out var flag))
                {
                    if (setting.Key == "console") settings.LogConsole = flag; else settings.LogFile = flag;
                }
                else
                {
                    fail($"{Quote(setting, raw)} is not a valid boolean; use true or false.{CommentHint(raw)}");
                }
                break;

            case "max_file_size_mb":
            case "retain_days":
                if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0)
                {
                    if (setting.Key == "retain_days") settings.RetainDays = number; else settings.MaxFileSizeMb = number;
                }
                else
                {
                    fail($"{Quote(setting, raw)} is not valid; use a whole number greater than 0.{CommentHint(raw)}");
                }
                break;
        }

        return null;
    }

    /// <summary>Validates a [paths] value and returns it resolved against the home directory.</summary>
    private static string? ValidatePath(ServerSchema.Setting setting, string raw, ServerConfigReport report, Action<string> fail)
    {
        var settings = report.Settings;
        var resolved = CheckPath(setting, raw, report.Home.Path, fail);

        switch (setting.Key)
        {
            case "data": settings.Data = raw; settings.DataPath = resolved; break;
            case "logs": settings.Logs = raw; settings.LogsPath = resolved; break;
            case "public": settings.Public = raw; settings.PublicPath = resolved; break;
            case "ai": settings.Ai = raw; settings.AiPath = resolved; break;
            case "backups": settings.Backups = raw; settings.BackupsPath = resolved; break;
        }

        return resolved;
    }
}
