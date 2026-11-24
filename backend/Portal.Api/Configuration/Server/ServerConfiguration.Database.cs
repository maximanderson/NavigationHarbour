using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Portal.Api.Configuration.Server;

/// <summary>
/// Stage 3A: one effective database location.
///
/// Location, in priority order (the existing configuration precedence applies):
///   1. A ConnectionStrings:PortalDb from the environment, command line or user secrets.
///   2. [paths] data from server.ini, when written there: &lt;data&gt;/usage.db.
///   3. ConnectionStrings:PortalDb from appsettings*.json (shipped default "Data Source=usage.db").
///   4. Built-in default "usage.db" (when no configuration supplies one).
/// Automatic discovery (2, 3, 4 only, when the file name is the default usage.db): if only the legacy
/// portalcore.db exists in that directory it keeps being used; if both exist startup is blocked. An explicit
/// connection string (1) is never second-guessed. No database file is ever renamed, moved or deleted.
/// A relative path is resolved against Navigation Harbour home, never the working directory.
/// </summary>
public static partial class ServerConfiguration
{
    public const string ConnectionStringKey = "ConnectionStrings:PortalDb";

    /// <summary>
    /// True only when the winning provider for the connection string is the one carrying the server.ini mapping.
    /// Decided by provider, not by comparing values: an explicit connection string that happens to equal the
    /// server.ini-derived one is still an explicit choice and must not be second-guessed.
    /// </summary>
    private static bool IsSuppliedByMapping(
        ConfigurationManager config,
        IReadOnlyDictionary<string, string?> mapped,
        IConfigurationSource? mappedSource)
    {
        if (mappedSource is null || !mapped.ContainsKey(ConnectionStringKey))
        {
            return false;
        }

        var index = config.Sources.IndexOf(mappedSource);
        var providers = ((IConfigurationRoot)config).Providers.ToList();
        if (index < 0 || index >= providers.Count)
        {
            return false;
        }

        for (var i = providers.Count - 1; i >= 0; i--)
        {
            if (providers[i].TryGet(ConnectionStringKey, out _))
            {
                return i == index;
            }
        }

        return false;
    }

    private static DatabaseLocation ResolveDatabase(
        ConfigurationManager config,
        ServerConfigReport report,
        IReadOnlyDictionary<string, string?> mapped,
        IConfigurationSource? mappedSource)
    {
        var isDevelopment = string.Equals(report.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
        var effective = config[ConnectionStringKey];

        string origin;
        bool isExplicit;
        bool discover;

        if (effective is not null && IsSuppliedByMapping(config, mapped, mappedSource))
        {
            origin = "server.ini [paths] data";
            isExplicit = true;
            discover = true;
        }
        else if (effective is null)
        {
            origin = "built-in default";
            isExplicit = false;
            discover = true;
            effective = "Data Source=" + DatabaseLocation.FileName;
        }
        else
        {
            var source = SourceOf((IConfigurationRoot)config, ConnectionStringKey);
            // appsettings*.json carry the shipped default; anything else was chosen on purpose.
            isExplicit = !source.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase);
            origin = isExplicit ? $"connection string ({source})" : $"{source} (shipped default)";
            discover = !isExplicit;
        }

        DatabaseLocation Unresolved(string message)
        {
            // Connection strings can hold credentials, so the value is never quoted.
            report.Errors.Add($"[database] {message}");
            return new DatabaseLocation { Origin = origin, IsExplicit = isExplicit };
        }

        if (string.IsNullOrWhiteSpace(effective))
        {
            return Unresolved($"{ConnectionStringKey} is empty.");
        }

        SqliteConnectionStringBuilder builder;
        try
        {
            builder = new SqliteConnectionStringBuilder(effective);
        }
        catch (ArgumentException)
        {
            return Unresolved($"{ConnectionStringKey} is not a valid SQLite connection string (value not shown).");
        }

        // SqliteConnectionStringBuilder.ContainsKey is true for every valid keyword, so the keywords
        // actually present are read with a plain builder.
        var supplied = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = effective };
        if (supplied.ContainsKey("Mode"))
        {
            return Unresolved($"{ConnectionStringKey} must not set Mode; {ProductInfo.Name} decides whether the database file may be created.");
        }

        var dataSource = builder.DataSource?.Trim();
        if (string.IsNullOrEmpty(dataSource) ||
            dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase) ||
            dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return Unresolved($"{ConnectionStringKey} must name a database file in 'Data Source' (in-memory and URI forms are not supported).");
        }

        if (!Path.IsPathRooted(dataSource) && report.Home.Path is null)
        {
            return Unresolved(
                $"The database location is relative but the {ProductInfo.Name} home directory is unknown. " +
                "Use --home <directory> or set PORTAL_HOME, or configure an absolute path.");
        }

        string filePath;
        try
        {
            filePath = Path.GetFullPath(PortalPaths.ResolveAgainstHome(report.Home.Path, dataSource));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Unresolved("The database location is not a valid path.");
        }

        if (Directory.Exists(filePath))
        {
            return Unresolved($"The database location {filePath} is a directory, not a file.");
        }

        if (discover && string.Equals(Path.GetFileName(filePath), DatabaseLocation.FileName, StringComparison.OrdinalIgnoreCase))
        {
            var legacyPath = Path.Combine(Path.GetDirectoryName(filePath)!, DatabaseLocation.LegacyFileName);
            var hasCurrent = File.Exists(filePath);
            var hasLegacy = File.Exists(legacyPath);

            if (hasCurrent && hasLegacy)
            {
                return new DatabaseLocation
                {
                    FilePath = filePath,
                    Origin = origin,
                    IsExplicit = isExplicit,
                    State = DatabaseState.Ambiguous,
                    Blocker =
                        $"Both {filePath} and {legacyPath} exist, so it is not clear which database to use. " +
                        "Nothing was changed. Choose one explicitly: set the full file path in ConnectionStrings:PortalDb " +
                        $"(for example PORTAL_ConnectionStrings__PortalDb=\"Data Source=<full path>\"), " +
                        "or move the database you do not want to use out of the data directory.",
                };
            }

            if (hasLegacy)
            {
                filePath = legacyPath;
                origin += $" (existing legacy file name {DatabaseLocation.LegacyFileName})";
            }
        }

        var (state, size) = InspectDatabaseFile(filePath);
        var (createOnStartup, blocker) = DecideDatabasePolicy(state, filePath, origin, isExplicit, isDevelopment);

        builder.DataSource = filePath;
        builder.Mode = SqliteOpenMode.ReadWrite;
        var runtime = builder.ToString();
        builder.Mode = SqliteOpenMode.ReadWriteCreate;
        var create = builder.ToString();

        return new DatabaseLocation
        {
            FilePath = filePath,
            Origin = origin,
            IsExplicit = isExplicit,
            State = state,
            SizeBytes = size,
            CreateOnStartup = createOnStartup,
            Blocker = blocker,
            RuntimeConnectionString = runtime,
            CreateConnectionString = create,
        };
    }
}
