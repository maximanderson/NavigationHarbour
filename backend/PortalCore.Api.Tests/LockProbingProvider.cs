using PortalCore.Api.Backup;
using PortalCore.Api.Backup.Sqlite;

namespace PortalCore.Api.Tests;

/// <summary>Wraps the SQLite provider and records whether the maintenance lock was held while it did its work.</summary>
public sealed class LockProbingProvider : IDatabaseBackupProvider
{
    private readonly SqliteBackupProvider _inner;
    private readonly string _file;

    public LockProbingProvider(SqliteBackupProvider inner, string file)
    {
        _inner = inner;
        _file = file;
    }

    public bool LockWasHeldDuringBackup { get; private set; }
    public bool LockWasHeldDuringRestore { get; private set; }

    /// <summary>True when something (this process included) currently holds the lock.</summary>
    public bool IsLockedAfterwards() => IsLocked();

    private bool IsLocked()
    {
        try
        {
            using var probe = MaintenanceLock.AcquireExclusive(_file);
            return false;
        }
        catch (MaintenanceLockBusyException)
        {
            return true;
        }
    }

    public string DatabaseId => _inner.DatabaseId;
    public string ProviderType => _inner.ProviderType;
    public string BackupFileExtension => _inner.BackupFileExtension;
    public string Description => _inner.Description;
    public IReadOnlyCollection<string> ProtectedPaths => _inner.ProtectedPaths;
    public bool DatabaseExists => _inner.DatabaseExists;

    public IDisposable AcquireMaintenanceLock(bool createDirectory) => _inner.AcquireMaintenanceLock(createDirectory);

    public Task EnsureRestoreAllowedAsync(CancellationToken c) => _inner.EnsureRestoreAllowedAsync(c);

    public async Task BackupAsync(string destinationFile, CancellationToken c)
    {
        LockWasHeldDuringBackup = IsLocked();
        await _inner.BackupAsync(destinationFile, c);
    }

    public Task VerifyAsync(string backupFile, CancellationToken c) => _inner.VerifyAsync(backupFile, c);

    public async Task RestoreAsync(string backupFile, CancellationToken c)
    {
        LockWasHeldDuringRestore = IsLocked();
        await _inner.RestoreAsync(backupFile, c);
    }
}
