using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Ini;
using Microsoft.Extensions.FileProviders;

namespace Portal.Api.Configuration.Server;

/// <summary>
/// Adds the private server.ini to the application configuration and validates it.
///
/// Precedence, lowest to highest:
///   appsettings.json, appsettings.{Environment}.json, conf/server.ini, user secrets (Development),
///   environment variables, PORTAL_ (and legacy PORTALCORE_) environment variables, command line.
/// These variables use "__" for sections, e.g. PORTAL_server__port=5300. When both prefixes set
/// the same key, PORTAL_ wins.
///
/// Stage 2: [log], [paths] logs and [paths] ai are applied (see ServerConfiguration.Apply.cs).
/// Everything else is validated and printed only.
/// </summary>
public static partial class ServerConfiguration
{
    public const string EnvironmentPrefix = "PORTAL_";

    /// <summary>Prefix used before the product was renamed; still honoured, but EnvironmentPrefix wins.</summary>
    public const string LegacyEnvironmentPrefix = "PORTALCORE_";
    public const int ExitInvalid = 2;
    public const string ConfigFolder = "conf";
    public const string ConfigFileName = "server.ini";
    public const string SampleFileName = "server.sample.ini";

    /// <summary>
    /// Registers the server.ini and PORTAL_/PORTALCORE_ sources on <paramref name="config"/> and returns
    /// the validation report. Never starts anything and never touches the database.
    /// </summary>
    public static ServerConfigReport Load(
        ConfigurationManager config,
        StartupOptions startup,
        string environmentName,
        string baseDirectory,
        string? environmentHome,
        string contentRoot,
        string environmentHomeName = PortalHome.EnvironmentVariable)
    {
        var isDevelopment = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
        var home = PortalHome.Resolve(startup.Home, environmentHome, baseDirectory, environmentHomeName);

        var configPath = home.Path is null
            ? null
            : Path.Combine(home.Path, ConfigFolder, ConfigFileName);

        var ini = configPath is null ? null : ServerIniFile.Read(configPath);

        var report = new ServerConfigReport
        {
            Home = home,
            ConfigPath = configPath,
            ContentRoot = contentRoot,
            FileFound = ini is { Exists: true },
            EnvironmentName = environmentName,
        };

        report.Errors.AddRange(startup.Errors);
        CheckHome(report, home, isDevelopment, startup);
        CheckFilePresence(report, ini, configPath, isDevelopment, startup);

        if (ini is not null)
        {
            report.Errors.AddRange(ini.Errors);
            if (ini.IsUsable)
            {
                CheckIniSections(ini, report);
            }
        }

        // Source position decides precedence.
        if (ini is { IsUsable: true })
        {
            // The source gets its own file provider rooted at the conf directory. Otherwise the
            // path would be resolved against the host content root (the working directory when
            // run from source), which would make loading depend on where the process was started.
            var confDirectory = Path.GetDirectoryName(ini.Path)!;
            InsertAfterAppSettings(
                config,
                new IniConfigurationSource
                {
                    FileProvider = new PhysicalFileProvider(confDirectory),
                    Path = Path.GetFileName(ini.Path),
                    Optional = false,
                    ReloadOnChange = false,
                });
        }

        // Legacy first, new second: a later source wins, so PORTAL_ overrides PORTALCORE_.
        InsertBeforeCommandLine(config, new EnvironmentVariablesConfigurationSource { Prefix = LegacyEnvironmentPrefix });
        InsertBeforeCommandLine(config, new EnvironmentVariablesConfigurationSource { Prefix = EnvironmentPrefix });

        Validate(config, report);
        Apply(config, report, contentRoot);
        return report;
    }

    private static void CheckHome(ServerConfigReport report, PortalHomeResult home, bool isDevelopment, StartupOptions startup)
    {
        if (home.Path is null)
        {
            if (isDevelopment || startup.AllowMissingConfig)
            {
                report.Warnings.Add($"{home.Problem} server.ini was not loaded.");
            }
            else
            {
                report.Errors.Add(home.Problem!);
            }
        }
        else if (home.Problem is not null)
        {
            // An explicit home that does not exist is always a mistake.
            report.Errors.Add(home.Problem);
        }
    }

    private static void CheckFilePresence(
        ServerConfigReport report,
        ServerIniFile? ini,
        string? configPath,
        bool isDevelopment,
        StartupOptions startup)
    {
        if (ini is not { Exists: false } || report.Home.Problem is not null)
        {
            return;
        }

        if (isDevelopment)
        {
            report.Notices.Add($"{configPath} not found; allowed in Development, built-in defaults are used.");
        }
        else if (startup.AllowMissingConfig)
        {
            report.Warnings.Add($"{configPath} not found; continuing because --allow-missing-config was given.");
        }
        else
        {
            report.Errors.Add(
                $"{configPath} not found. Copy {ConfigFolder}/{SampleFileName} to {ConfigFolder}/{ConfigFileName} " +
                "and edit it (or pass --allow-missing-config for testing).");
        }
    }
}
