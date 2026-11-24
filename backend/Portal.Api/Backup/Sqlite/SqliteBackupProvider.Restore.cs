using Portal.Api.Configuration.Server;

namespace Portal.Api.Backup.Sqlite;

public sealed partial class SqliteBackupProvider
{
    /// <summary>Infix of the files a restore creates: "&lt;db&gt;.restore-&lt;token&gt;.partial" and "&lt;db&gt;.restore-aside-&lt;token&gt;*".</summary>
    private const string AsideInfix = ".restore-aside-";

    private sealed class NoLock : IDisposable
    {
        public static readonly NoLock Instance = new();

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Takes the exclusive maintenance lock next to the database file. In a check-only run
    /// (<paramref name="createDirectory"/> false) a missing database directory means nothing exists that
    /// could be in use, so no lock is needed and nothing is created.
    /// </summary>
    public IDisposable AcquireMaintenanceLock(bool createDirectory)
    {
        var directory = Path.GetDirectoryName(_filePath)!;

        if (!Directory.Exists(directory))
        {
            if (!createDirectory)
            {
                return NoLock.Instance;
            }

            var parent = Path.GetDirectoryName(directory);
            if (parent is null || !Directory.Exists(parent))
            {
                throw new BackupException(
                    $"Neither {directory} nor its parent directory exists. Create the parent directory first.");
            }

            Directory.CreateDirectory(directory);
        }

        return MaintenanceLock.AcquireExclusive(_filePath);
    }

    /// <summary>
    /// Provider precondition for a restore (the caller holds the maintenance lock): the live path must be
    /// missing, empty or a SQLite database, and no earlier interrupted restore may have left the original
    /// database aside. Whether a process is using the database is decided by the maintenance lock, not here:
    /// a SQL probe cannot reliably detect idle WAL-mode connections.
    /// </summary>
    public Task EnsureRestoreAllowedAsync(CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var (state, _) = ServerConfiguration.InspectDatabaseFile(_filePath);

            switch (state)
            {
                case DatabaseState.NotSqlite:
                    throw new BackupException(
                        $"The file at {_filePath} exists but is not a SQLite database. It will not be overwritten; move it away first.");

                case DatabaseState.Unreadable:
                    throw new BackupException(
                        $"The database file {_filePath} cannot be read (permissions or I/O error); it will not be replaced.");

                case DatabaseState.DirectoryMissing:
                    var parent = Path.GetDirectoryName(Path.GetDirectoryName(_filePath)!);
                    if (parent is null || !Directory.Exists(parent))
                    {
                        throw new BackupException(
                            $"Neither {Path.GetDirectoryName(_filePath)} nor its parent directory exists. Create the parent directory first.");
                    }
                    break;
            }

            var directory = Path.GetDirectoryName(_filePath)!;
            if (Directory.Exists(directory) &&
                Directory.GetFiles(directory, Path.GetFileName(_filePath) + AsideInfix + "*").Length > 0)
            {
                throw new BackupException(
                    $"A previous restore was interrupted: files named {Path.GetFileName(_filePath)}{AsideInfix}* in {directory} hold the original database. " +
                    "Recover them first (docs/backup-restore.md, \"After an interrupted restore\"). Nothing was changed.");
            }
        }, cancellationToken);
}
