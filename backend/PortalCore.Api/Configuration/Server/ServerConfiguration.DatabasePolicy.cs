namespace PortalCore.Api.Configuration.Server;

public static partial class ServerConfiguration
{
    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();

    /// <summary>
    /// Looks at the file system only (existence, size, the 16-byte SQLite header). The file is never
    /// opened with SQLite, so nothing can be created, locked or modified. Read access allows sharing,
    /// so a running instance holding the file does not matter.
    /// </summary>
    internal static (DatabaseState State, long Size) InspectDatabaseFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                var directory = Path.GetDirectoryName(filePath);
                return (directory is not null && Directory.Exists(directory)
                    ? DatabaseState.Missing
                    : DatabaseState.DirectoryMissing, 0);
            }

            using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length == 0)
            {
                return (DatabaseState.EmptyFile, 0);
            }

            var header = new byte[SqliteHeader.Length];
            var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

            return (read == header.Length && header.AsSpan().SequenceEqual(SqliteHeader)
                ? DatabaseState.Exists
                : DatabaseState.NotSqlite, stream.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (DatabaseState.Unreadable, 0);
        }
    }

    /// <summary>
    /// A new empty database is created automatically only in Development, at the default location.
    /// Anything else that is not an existing SQLite file stops startup with an explanation.
    /// </summary>
    private static (bool CreateOnStartup, string? Blocker) DecideDatabasePolicy(
        DatabaseState state,
        string path,
        string origin,
        bool isExplicit,
        bool isDevelopment)
    {
        switch (state)
        {
            case DatabaseState.Exists:
                return (false, null);

            case DatabaseState.NotSqlite:
                return (false,
                    $"The file {path} exists but is not a SQLite database. It has been left untouched; " +
                    "restore the correct file or choose another location.");

            case DatabaseState.Unreadable:
                return (false,
                    $"The database file {path} cannot be read (permissions or I/O error). " +
                    "Check that the Portal Core account has Modify access to the data directory.");
        }

        // Missing, EmptyFile or DirectoryMissing from here on.
        if (isDevelopment && !isExplicit && state != DatabaseState.DirectoryMissing)
        {
            return (true, null);
        }

        var what = state switch
        {
            DatabaseState.Missing => $"Database file not found: {path}",
            DatabaseState.EmptyFile => $"Database file is empty (0 bytes): {path}",
            _ => $"Database directory not found: {Path.GetDirectoryName(path)} (database file {path})",
        };

        var why = !isDevelopment
            ? "Automatic database creation is disabled outside Development, so no empty database will be created."
            : isExplicit
                ? $"The location was set explicitly ({origin}), so Portal Core will not create a replacement database."
                : "The default development directory does not exist.";

        return (false,
            $"{what}. {why} For a new installation create the database deliberately with --init-db; " +
            $"otherwise restore it or correct the location ({origin}).");
    }
}
