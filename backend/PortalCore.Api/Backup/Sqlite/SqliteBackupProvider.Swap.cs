using Microsoft.Data.Sqlite;

namespace PortalCore.Api.Backup.Sqlite;

public sealed partial class SqliteBackupProvider
{
    private static readonly string[] Sidecars = { "", "-wal", "-shm", "-journal" };

    /// <summary>Test hook: called after the originals were moved aside and before the replacement is put in place.</summary>
    internal Action? FaultAfterOriginalsMovedAside { get; set; }

    /// <summary>
    /// Replaces the live database. The caller holds the exclusive maintenance lock and has taken a verified
    /// pre-restore backup. Steps:
    /// 1. Build the replacement next to the live file from the backup and verify it; the live database is untouched.
    /// 2. Close every SQLite connection of this process (none is pooled; pools are cleared as well).
    /// 3. Rename the live .db/-wal/-shm/-journal aside (never deleted).
    /// 4. Rename the replacement into place.
    /// 5. On any failure in 3-4 put the originals back. On success delete the aside copies.
    /// Each rename is atomic, the sequence is NOT: a crash between 3 and 4 leaves the originals as
    /// "&lt;db&gt;.restore-aside-*" files, and the next restore refuses to run until they are recovered by hand.
    /// </summary>
    public Task RestoreAsync(string backupFile, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var directory = Path.GetDirectoryName(_filePath)!;
            var token = Guid.NewGuid().ToString("N")[..8];
            var prepared = $"{_filePath}.restore-{token}.partial";
            var asideBase = _filePath + AsideInfix + token;
            var moved = new List<(string Original, string Aside)>();
            var placed = false;

            try
            {
                Directory.CreateDirectory(directory);

                // Leftovers of an earlier crashed attempt are safe to remove: we hold the exclusive lock.
                foreach (var stale in Directory.GetFiles(directory, Path.GetFileName(_filePath) + ".restore-*.partial*"))
                {
                    File.Delete(stale);
                }

                PrepareReplacement(backupFile, prepared, WalDesired(), cancellationToken);
                var preparedSize = new FileInfo(prepared).Length;

                // Last point where cancellation is honoured; from here the swap runs to its end.
                cancellationToken.ThrowIfCancellationRequested();
                SqliteConnection.ClearAllPools();

                foreach (var suffix in Sidecars)
                {
                    var original = _filePath + suffix;
                    if (File.Exists(original))
                    {
                        File.Move(original, asideBase + suffix, overwrite: false);
                        moved.Add((original, asideBase + suffix));
                    }
                }

                FaultAfterOriginalsMovedAside?.Invoke();

                File.Move(prepared, _filePath, overwrite: false);
                placed = true;

                if (new FileInfo(_filePath).Length != preparedSize)
                {
                    throw new IOException("The restored database file has an unexpected size.");
                }
            }
            catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or BackupException or OperationCanceledException)
            {
                RollBack(moved, prepared, placed, asideBase);

                if (ex is BackupException or OperationCanceledException)
                {
                    throw;
                }

                var detail = ex is SqliteException sx ? Describe(sx) : $"{ex.GetType().Name}: {ex.Message}";
                throw new BackupException($"Restore failed: {detail}. The original database was put back and not changed.", ex);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
            }

            // Success: the verified pre-restore backup is the recovery copy, so the aside copies are redundant.
            foreach (var (_, aside) in moved)
            {
                try
                {
                    File.Delete(aside);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Left behind on purpose; the next restore refuses to run until they are dealt with.
                }
            }
        }, cancellationToken);

    /// <summary>Puts the originals back. If that is impossible the message says exactly where they are.</summary>
    private void RollBack(List<(string Original, string Aside)> moved, string prepared, bool placed, string asideBase)
    {
        DeleteWithSidecars(prepared);

        if (moved.Count == 0)
        {
            return;
        }

        try
        {
            if (placed && File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            foreach (var (original, aside) in moved)
            {
                if (File.Exists(aside) && !File.Exists(original))
                {
                    File.Move(aside, original, overwrite: false);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BackupException(
                $"Restore failed and the original database could not be put back automatically ({ex.Message}). " +
                $"The original files are kept as {asideBase}* in {Path.GetDirectoryName(_filePath)}; rename them back by hand " +
                "(docs/backup-restore.md, \"After an interrupted restore\"). The pre-restore backup is also available.", ex);
        }
    }
}
