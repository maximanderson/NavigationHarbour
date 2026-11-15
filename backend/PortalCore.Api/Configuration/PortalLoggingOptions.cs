using Serilog.Events;

namespace PortalCore.Api.Configuration;

/// <summary>
/// System (diagnostic) logging settings read from the "PortalLogging" section.
/// Independent of the Usage Monitor and of any AI analysis.
/// Parsing is deliberately tolerant: invalid values fall back to safe defaults
/// and are reported through <see cref="Issues"/> instead of failing startup.
/// </summary>
public sealed class PortalLoggingOptions
{
    public const string SectionName = "PortalLogging";
    public const string DefaultDirectory = "logs";
    public const int DefaultMaxFileSizeMb = 10;
    public const int DefaultRetainDays = 14;

    public bool Enabled { get; private set; } = true;

    /// <summary>
    /// Serilog threshold. "error" maps to Error, which also includes Critical (Fatal).
    /// </summary>
    public LogEventLevel Level { get; private set; } = LogEventLevel.Information;

    public bool Console { get; private set; } = true;
    public bool FileEnabled { get; private set; } = true;
    public string Directory { get; private set; } = DefaultDirectory;
    public int MaxFileSizeMb { get; private set; } = DefaultMaxFileSizeMb;
    public int RetainDays { get; private set; } = DefaultRetainDays;

    /// <summary>Problems found while reading the configuration (defaults were used).</summary>
    public List<string> Issues { get; } = new();

    public static PortalLoggingOptions Parse(IConfiguration configuration)
    {
        var options = new PortalLoggingOptions();
        var section = configuration.GetSection(SectionName);

        options.Enabled = ReadBool(section["Enabled"], options.Enabled, "Enabled", options.Issues);
        options.Console = ReadBool(section["Console"], options.Console, "Console", options.Issues);
        options.Level = ReadLevel(section["Level"], options.Level, options.Issues);

        var file = section.GetSection("File");
        options.FileEnabled = ReadBool(file["Enabled"], options.FileEnabled, "File:Enabled", options.Issues);
        options.MaxFileSizeMb = ReadPositiveInt(file["MaxFileSizeMb"], options.MaxFileSizeMb, "File:MaxFileSizeMb", options.Issues);
        options.RetainDays = ReadPositiveInt(file["RetainDays"], options.RetainDays, "File:RetainDays", options.Issues);

        var directory = file["Directory"];
        if (!string.IsNullOrWhiteSpace(directory))
        {
            options.Directory = directory.Trim();
        }
        else if (directory is not null)
        {
            options.Issues.Add($"{SectionName}:File:Directory is empty; using \"{DefaultDirectory}\".");
        }

        return options;
    }

    private static bool ReadBool(string? raw, bool fallback, string key, List<string> issues)
    {
        if (raw is null) return fallback;
        if (bool.TryParse(raw.Trim(), out var value)) return value;

        issues.Add($"{SectionName}:{key} has invalid value \"{raw}\"; using \"{fallback}\".");
        return fallback;
    }

    private static int ReadPositiveInt(string? raw, int fallback, string key, List<string> issues)
    {
        if (raw is null) return fallback;
        if (int.TryParse(raw.Trim(), out var value) && value > 0) return value;

        issues.Add($"{SectionName}:{key} has invalid value \"{raw}\"; using {fallback}.");
        return fallback;
    }

    private static LogEventLevel ReadLevel(string? raw, LogEventLevel fallback, List<string> issues)
    {
        if (raw is null) return fallback;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "debug": return LogEventLevel.Debug;
            case "info": return LogEventLevel.Information;
            case "warning": return LogEventLevel.Warning;
            case "error": return LogEventLevel.Error;
            default:
                issues.Add($"{SectionName}:Level has invalid value \"{raw}\"; using \"info\". Supported: debug, info, warning, error.");
                return fallback;
        }
    }
}
