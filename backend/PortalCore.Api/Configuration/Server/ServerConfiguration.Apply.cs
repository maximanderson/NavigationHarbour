using Microsoft.Extensions.Configuration;

namespace PortalCore.Api.Configuration.Server;

/// <summary>
/// Stage 2: translates explicitly configured [log], [paths] logs and [paths] ai values into the
/// existing PortalLogging:* and AiSettings:* keys, so the existing logging setup and AI services
/// are reused unchanged. Values that are not configured are NOT mapped, so development keeps the
/// appsettings.json defaults (logs/, Data/knowledge, Data/prompts, relative to the content root).
/// </summary>
public static partial class ServerConfiguration
{
    private const string LoggingPrefix = PortalLoggingOptions.SectionName;

    private static void Apply(ConfigurationManager config, ServerConfigReport report, string contentRoot)
    {
        var mapped = report.IsValid ? BuildMapping(report) : new Dictionary<string, string?>();

        if (mapped.Count > 0)
        {
            // Same level as server.ini: above appsettings*.json, below user secrets,
            // environment variables and the command line.
            InsertAfterAppSettings(config, new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource
            {
                InitialData = mapped,
            });

            foreach (var (key, value) in mapped)
            {
                var effective = config[key];
                if (!string.Equals(effective, value, StringComparison.OrdinalIgnoreCase))
                {
                    // Values are never quoted: a connection string can hold credentials.
                    report.Warnings.Add(
                        $"{key} is overridden by an environment variable, user secret or command-line argument; " +
                        "the corresponding server.ini setting is not in effect" +
                        (key == ConnectionStringKey ? " ([paths] data is ignored; the connection string decides the database file)." : "."));
                }
            }
        }

        ShowEffectiveLocations(config, report, contentRoot);
        report.Database = ResolveDatabase(config, report, mapped);
        ShowEffectiveDatabase(report);
        CheckBackupDirectoryAgainstDatabase(report);
    }

    /// <summary>The backup directory must not be, or contain, the directory of the effective database.</summary>
    private static void CheckBackupDirectoryAgainstDatabase(ServerConfigReport report)
    {
        var backups = report.Settings.BackupsPath;
        var databaseDirectory = report.Database?.FilePath is { } file ? Path.GetDirectoryName(file) : null;

        if (backups is not null && databaseDirectory is not null && PortalPaths.IsSameOrUnder(databaseDirectory, backups))
        {
            report.Errors.Add(
                $"[paths] backups: resolves to {backups}, which is or contains the database directory {databaseDirectory}. " +
                "Backups must be kept in a separate directory.");
        }
    }

    /// <summary>--print-config shows the database file that will really be used.</summary>
    private static void ShowEffectiveDatabase(ServerConfigReport report)
    {
        if (report.Database?.FilePath is null)
        {
            return;
        }

        for (var i = 0; i < report.Values.Count; i++)
        {
            if (report.Values[i] is { Section: "paths", Key: "data" } v)
            {
                report.Values[i] = v with { ResolvedPath = $"database file: {report.Database.FilePath}" };
            }
        }
    }

    private static Dictionary<string, string?> BuildMapping(ServerConfigReport report)
    {
        var s = report.Settings;
        var mapped = new Dictionary<string, string?>();

        bool IsConfigured(string section, string key) =>
            report.Values.Any(v => v.Section == section && v.Key == key && v.Source != "default");

        if (IsConfigured("log", "level")) mapped[$"{LoggingPrefix}:Level"] = s.LogLevel;
        if (IsConfigured("log", "console")) mapped[$"{LoggingPrefix}:Console"] = Bool(s.LogConsole);
        if (IsConfigured("log", "file")) mapped[$"{LoggingPrefix}:File:Enabled"] = Bool(s.LogFile);
        if (IsConfigured("log", "max_file_size_mb")) mapped[$"{LoggingPrefix}:File:MaxFileSizeMb"] = s.MaxFileSizeMb.ToString();
        if (IsConfigured("log", "retain_days")) mapped[$"{LoggingPrefix}:File:RetainDays"] = s.RetainDays.ToString();

        if (IsConfigured("paths", "logs") && s.LogsPath is not null)
        {
            mapped[$"{LoggingPrefix}:File:Directory"] = s.LogsPath;
        }

        if (IsConfigured("paths", "data") && s.DataPath is not null)
        {
            // The database file name is fixed; only its directory is configurable.
            mapped[ConnectionStringKey] = "Data Source=" + Path.Combine(s.DataPath, DatabaseLocation.FileName);
        }

        if (IsConfigured("paths", "ai") && s.AiPath is not null)
        {
            mapped["AiSettings:KnowledgeFolder"] = Path.Combine(s.AiPath, "knowledge");
            mapped["AiSettings:PromptFolder"] = Path.Combine(s.AiPath, "prompts");
        }

        return mapped;
    }

    /// <summary>
    /// Replaces the reported paths with what the application will really use, including values
    /// that come from appsettings.json (relative to the content root) rather than server.ini.
    /// </summary>
    private static void ShowEffectiveLocations(ConfigurationManager config, ServerConfigReport report, string contentRoot)
    {
        var logs = PortalPaths.ResolveAgainstHome(
            contentRoot,
            config[$"{LoggingPrefix}:File:Directory"] is { Length: > 0 } dir ? dir : PortalLoggingOptions.DefaultDirectory);

        var knowledge = PortalPaths.ResolveAgainstHome(contentRoot, config["AiSettings:KnowledgeFolder"] ?? "Data/knowledge");
        var prompts = PortalPaths.ResolveAgainstHome(contentRoot, config["AiSettings:PromptFolder"] ?? "Data/prompts");

        report.Settings.LogsPath = logs;

        for (var i = 0; i < report.Values.Count; i++)
        {
            var v = report.Values[i];
            if (v is { Section: "paths", Key: "logs" })
            {
                report.Values[i] = v with { ResolvedPath = logs };
            }
            else if (v is { Section: "paths", Key: "ai" })
            {
                report.Values[i] = v with { ResolvedPath = $"knowledge: {knowledge}; prompts: {prompts}" };
            }
        }
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
