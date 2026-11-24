namespace Portal.Api.Backup;

/// <summary>
/// Database-specific backup and restore operations. The common framework (BackupService,
/// BackupStore, BackupCommands) knows nothing about SQL or file formats; everything specific to
/// one database engine lives in an implementation of this interface.
/// Implementations report problems with <see cref="BackupException"/> (message safe to display).
/// </summary>
public interface IDatabaseBackupProvider
{
    /// <summary>Stable identifier used on the command line and in backup file names, e.g. "portal".</summary>
    string DatabaseId { get; }

    /// <summary>Kind of database, recorded in the backup metadata, e.g. "sqlite".</summary>
    string ProviderType { get; }

    /// <summary>Extension of a backup file, including the dot, e.g. ".db".</summary>
    string BackupFileExtension { get; }

    /// <summary>Human-readable location of the live database, for messages. Never contains secrets.</summary>
    string Description { get; }

    /// <summary>Full paths of the live database's own files; backups must never be written to or restored from them.</summary>
    IReadOnlyCollection<string> ProtectedPaths { get; }

    /// <summary>True when the live database currently exists (a backup of a missing database is refused).</summary>
    bool DatabaseExists { get; }

    /// <summary>
    /// Takes the exclusive cross-process maintenance lock of the live database (see <see cref="MaintenanceLock"/>).
    /// Throws <see cref="MaintenanceLockBusyException"/> without changing anything when Navigation Harbour, or another
    /// restore, holds it. The caller keeps the lock for the whole restore and disposes it afterwards.
    /// <paramref name="createDirectory"/> allows creating a missing database directory (real restores only).
    /// </summary>
    IDisposable AcquireMaintenanceLock(bool createDirectory);

    /// <summary>
    /// Provider-specific restore precondition, run (with the lock held) before anything is changed:
    /// throws <see cref="BackupException"/> when the live database must not be replaced, for example
    /// because the file is not a database of this kind.
    /// </summary>
    Task EnsureRestoreAllowedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes a consistent snapshot of the live database to <paramref name="destinationFile"/> (a new file).
    /// Must work while the database is in use, must not modify the source, and must never create a missing source.
    /// Throws <see cref="BackupSourceMissingException"/> when the source database does not exist.
    /// On failure it removes whatever it wrote.
    /// </summary>
    Task BackupAsync(string destinationFile, CancellationToken cancellationToken);

    /// <summary>Checks that a backup file is a usable backup of this kind of database. Throws when it is not.</summary>
    Task VerifyAsync(string backupFile, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the live database content with the backup. The caller holds the maintenance lock, has already
    /// verified the backup and taken a safety copy. The replacement is prepared and verified before the live
    /// database is touched; if the swap fails the provider puts the original back. This is not atomic across
    /// files, so a crash mid-swap can leave files that must be recovered by hand (docs/backup-restore.md).
    /// </summary>
    Task RestoreAsync(string backupFile, CancellationToken cancellationToken);
}

/// <summary>A backup, verify or restore problem with a message that is safe to show to an administrator.</summary>
public class BackupException : Exception
{
    public BackupException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>The database to back up does not exist (it is never created by a backup).</summary>
public sealed class BackupSourceMissingException : BackupException
{
    public BackupSourceMissingException(string message) : base(message)
    {
    }
}
