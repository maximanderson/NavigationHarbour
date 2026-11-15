namespace PortalCore.Api.Configuration.Server;

public enum DatabaseState
{
    /// <summary>A SQLite file exists at the location.</summary>
    Exists,

    /// <summary>The directory exists but the file does not.</summary>
    Missing,

    /// <summary>The directory that should contain the file does not exist.</summary>
    DirectoryMissing,

    /// <summary>A zero-byte file exists (what a failed first open leaves behind). No data to lose.</summary>
    EmptyFile,

    /// <summary>A non-empty file exists but is not a SQLite database.</summary>
    NotSqlite,

    /// <summary>The file exists but cannot be read.</summary>
    Unreadable,

    /// <summary>The location could not be determined (see the report errors).</summary>
    Unresolved,
}

/// <summary>
/// The one effective SQLite location for the Usage Monitor database, decided once at startup and
/// used by --print-config, --init-db and the DbContext registration, so they can never disagree.
/// Determined without opening the file with SQLite.
/// </summary>
public sealed class DatabaseLocation
{
    public const string FileName = "portalcore.db";

    public string? FilePath { get; init; }

    /// <summary>Where the location came from, e.g. "server.ini [paths] data".</summary>
    public required string Origin { get; init; }

    /// <summary>
    /// True when the location was chosen on purpose (server.ini, environment variable, command line,
    /// user secrets), false for the shipped/built-in default.
    /// </summary>
    public bool IsExplicit { get; init; }

    public DatabaseState State { get; init; } = DatabaseState.Unresolved;
    public long SizeBytes { get; init; }

    /// <summary>A new empty database may be created at startup (Development, default location only).</summary>
    public bool CreateOnStartup { get; init; }

    /// <summary>Why normal startup must stop; null when startup may proceed.</summary>
    public string? Blocker { get; init; }

    /// <summary>Connection string for the running application: Mode=ReadWrite, SQLite never creates a file.</summary>
    public string? RuntimeConnectionString { get; init; }

    /// <summary>Connection string used only to create the database: Mode=ReadWriteCreate.</summary>
    public string? CreateConnectionString { get; init; }

    /// <summary>--init-db may create the database (nothing exists that could be overwritten).</summary>
    public bool CanInitialize =>
        FilePath is not null &&
        (State == DatabaseState.Missing || State == DatabaseState.DirectoryMissing || State == DatabaseState.EmptyFile);

    public string StateText => State switch
    {
        DatabaseState.Exists => $"exists, {SizeBytes} bytes",
        DatabaseState.Missing => "missing",
        DatabaseState.DirectoryMissing => "directory missing",
        DatabaseState.EmptyFile => "empty file (0 bytes)",
        DatabaseState.NotSqlite => "not a SQLite database",
        DatabaseState.Unreadable => "cannot be read",
        _ => "unresolved",
    };

    public string PolicyText =>
        State == DatabaseState.Exists ? "opened as-is; never recreated or migrated" :
        CreateOnStartup ? "a new empty database is created at startup (Development default location only)" :
        Blocker is not null ? "startup stops; create deliberately with --init-db or fix the location" :
        "unresolved";
}
