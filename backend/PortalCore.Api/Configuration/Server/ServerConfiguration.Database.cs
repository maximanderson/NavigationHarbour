using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace PortalCore.Api.Configuration.Server;

/// <summary>
/// Stage 3A: one effective database location.
///
/// Location, in priority order (the existing configuration precedence applies):
///   1. A ConnectionStrings:PortalDb from the environment, command line or user secrets.
///   2. [paths] data from server.ini, when written there: &lt;data&gt;/portalcore.db.
///   3. ConnectionStrings:PortalDb from appsettings*.json (shipped default "Data Source=portalcore.db").
///   4. Built-in default "portalcore.db" (when no configuration supplies one).
/// A relative path is resolved against Portal Core home, never the working directory.
/// </summary>
public static partial class ServerConfiguration
{
    public const string ConnectionStringKey = "ConnectionStrings:PortalDb";

    private static DatabaseLocation ResolveDatabase(
        ConfigurationManager config,
        ServerConfigReport report,
        IReadOnlyDictionary<string, string?> mapped)
    {
        var isDevelopment = string.Equals(report.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
        var effective = config[ConnectionStringKey];

        string origin;
        bool isExplicit;

        if (effective is not null && mapped.TryGetValue(ConnectionStringKey, out var fromIni) && fromIni == effective)
        {
            origin = "server.ini [paths] data";
            isExplicit = true;
        }
        else if (effective is null)
        {
            origin = "built-in default";
            isExplicit = false;
            effective = "Data Source=" + DatabaseLocation.FileName;
        }
        else
        {
            var source = SourceOf((IConfigurationRoot)config, ConnectionStringKey);
            // appsettings*.json carry the shipped default; anything else was chosen on purpose.
            isExplicit = !source.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase);
            origin = isExplicit ? $"connection string ({source})" : $"{source} (shipped default)";
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
            return Unresolved($"{ConnectionStringKey} must not set Mode; Portal Core decides whether the database file may be created.");
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
                "The database location is relative but the Portal Core home directory is unknown. " +
                "Use --home <directory> or set PORTALCORE_HOME, or configure an absolute path.");
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
