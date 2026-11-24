using Portal.Api.Configuration;
using Microsoft.Data.Sqlite;

namespace Portal.Api.Backup.Sqlite;

public sealed partial class SqliteBackupProvider
{
    /// <summary>True when the new database should be in WAL mode: a new database, or one that was in WAL mode.</summary>
    private bool WalDesired()
    {
        try
        {
            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length < 20)
            {
                return true;
            }

            // Bytes 18 and 19 of the SQLite header are the file-format write/read versions: 2 means WAL.
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var header = new byte[20];
            return stream.Read(header, 0, 20) == 20 && header[18] == 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// Builds and verifies the replacement as a standalone file. Every connection is closed (and pools cleared)
    /// before returning, so a checkpointed WAL database is one self-contained file.
    /// </summary>
    private void PrepareReplacement(string backupFile, string prepared, bool wal, CancellationToken cancellationToken)
    {
        try
        {
            using (var source = new SqliteConnection(ConnectionString(backupFile, SqliteOpenMode.ReadOnly)))
            using (var destination = new SqliteConnection(ConnectionString(prepared, SqliteOpenMode.ReadWriteCreate)))
            {
                source.Open();
                destination.Open();
                cancellationToken.ThrowIfCancellationRequested();

                source.BackupDatabase(destination);

                if (wal)
                {
                    Scalar(destination, "PRAGMA journal_mode = WAL;");
                    Scalar(destination, "PRAGMA wal_checkpoint(TRUNCATE);");
                }

                var integrity = Scalar(destination, "PRAGMA integrity_check;");
                if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BackupException("The replacement database failed SQLite's integrity check; nothing was changed.");
                }

                var tables = Scalar(destination,
                    $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{RequiredTable}';");
                if (tables != "1")
                {
                    throw new BackupException($"The replacement database is not a {ProductInfo.Name} database; nothing was changed.");
                }
            }
        }
        catch (SqliteException ex)
        {
            DeleteWithSidecars(prepared);
            throw new BackupException($"SQLite restore failed: {Describe(ex)}. The live database was not changed.", ex);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }

        // Closing the last connection of a WAL database checkpoints and removes -wal/-shm. Anything left must be empty.
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            var sidecar = prepared + suffix;
            if (!File.Exists(sidecar))
            {
                continue;
            }

            if (suffix == "-wal" && new FileInfo(sidecar).Length > 0)
            {
                throw new BackupException("The replacement database still has uncheckpointed WAL data; nothing was changed.");
            }

            File.Delete(sidecar);
        }
    }
}
