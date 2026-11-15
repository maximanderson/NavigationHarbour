namespace PortalCore.Api.Backup;

/// <summary>
/// Cross-process maintenance lock for one database, enforced by the operating system.
///
/// It is a lock on a dedicated file next to the database, "&lt;database file&gt;.maintenance.lock", so it is tied to
/// the effective database location rather than to the working directory or the application. Portal Core
/// holds it shared for as long as it may use the database; a restore holds it exclusive for the whole
/// operation, so the two can never overlap.
///
/// Mechanism: a <see cref="FileStream"/> opened with a sharing mode. On Windows this is the mandatory
/// share mode of the file system; on Linux/macOS .NET maps it to flock(2). The operating system releases the
/// lock when the process exits, however it exits, so the presence of the file means nothing and a stale file is harmless.
///
/// Limits (see docs/backup-restore.md): it only protects against processes that use this lock. It does not stop
/// an external SQLite client, and it relies on the file system honouring the lock (local disks do; some network
/// file systems do not). The lock file must never be deleted while a server is running.
/// </summary>
public sealed class MaintenanceLock : IDisposable
{
    public const string FileSuffix = ".maintenance.lock";

    private FileStream? _stream;

    private MaintenanceLock(string filePath, FileStream stream, bool isExclusive, bool isEnforced)
    {
        FilePath = filePath;
        _stream = stream;
        IsExclusive = isExclusive;
        IsEnforced = isEnforced;
    }

    public string FilePath { get; }

    public bool IsExclusive { get; }

    /// <summary>
    /// True when a self-check showed that the operating system really refuses a conflicting lock on this
    /// file. False when locking is switched off (DOTNET_SYSTEM_IO_DISABLEFILELOCKING) or unsupported here.
    /// </summary>
    public bool IsEnforced { get; }

    public static string PathFor(string databaseFile) => Path.GetFullPath(databaseFile) + FileSuffix;

    /// <summary>The server's lock: compatible with other shared locks, incompatible with a restore.</summary>
    public static MaintenanceLock AcquireShared(string databaseFile) => Acquire(databaseFile, exclusive: false);

    /// <summary>The restore's lock: fails while anything else holds the lock. Never waits.</summary>
    public static MaintenanceLock AcquireExclusive(string databaseFile) => Acquire(databaseFile, exclusive: true);

    private static MaintenanceLock Acquire(string databaseFile, bool exclusive)
    {
        var path = PathFor(databaseFile);

        FileStream stream;
        try
        {
            stream = new FileStream(
                path, FileMode.OpenOrCreate, FileAccess.ReadWrite, exclusive ? FileShare.None : FileShare.ReadWrite, 1, FileOptions.None);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new BackupException($"The maintenance lock cannot be created because the directory does not exist: {Path.GetDirectoryName(path)}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new BackupException($"The maintenance lock file {path} cannot be opened (permission denied).", ex);
        }
        catch (IOException ex)
        {
            throw new MaintenanceLockBusyException(
                $"The maintenance lock {path} is held by another process" +
                (exclusive ? " (Portal Core is running, or another restore is in progress)" : " (a restore is in progress)") +
                $". Detail: {ex.Message}", ex);
        }

        var enforced = IsOsEnforced(path, exclusive);
        if (!enforced && exclusive)
        {
            stream.Dispose();
            throw new BackupException(
                $"The operating system does not enforce file locks on {path} (DOTNET_SYSTEM_IO_DISABLEFILELOCKING set, " +
                "or a file system without lock support), so a restore cannot be protected from a running Portal Core. Nothing was changed.");
        }

        return new MaintenanceLock(path, stream, exclusive, enforced);
    }

    /// <summary>Asks for a conflicting lock on the same file; the OS must refuse it.</summary>
    private static bool IsOsEnforced(string path, bool heldExclusive)
    {
        try
        {
            using var probe = new FileStream(
                path, FileMode.Open, FileAccess.ReadWrite, heldExclusive ? FileShare.ReadWrite : FileShare.None, 1, FileOptions.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Releases the lock. The lock file is deliberately left in place (deleting it would let two inodes be locked).</summary>
    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
    }
}

/// <summary>The maintenance lock could not be obtained because another process holds it.</summary>
public sealed class MaintenanceLockBusyException : BackupException
{
    public MaintenanceLockBusyException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
