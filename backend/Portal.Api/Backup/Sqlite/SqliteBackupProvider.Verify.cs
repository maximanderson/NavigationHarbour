using Portal.Api.Configuration;
using Microsoft.Data.Sqlite;

namespace Portal.Api.Backup.Sqlite;

public sealed partial class SqliteBackupProvider
{
    /// <summary>The file opens as a SQLite database, passes the integrity check and is a Navigation Harbour database.</summary>
    public Task VerifyAsync(string backupFile, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString(backupFile, SqliteOpenMode.ReadOnly));
                connection.Open();

                var integrity = Scalar(connection, "PRAGMA integrity_check;");
                if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BackupException("The backup failed SQLite's integrity check; it is damaged.");
                }

                var tables = Scalar(connection,
                    $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{RequiredTable}';");
                if (tables != "1")
                {
                    throw new BackupException($"The file is a SQLite database but not a {ProductInfo.Name} database (table {RequiredTable} not found).");
                }
            }
            catch (SqliteException ex)
            {
                throw new BackupException($"The file is not a usable SQLite backup: {Describe(ex)}", ex);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
            }
        }, cancellationToken);

    private static string Describe(SqliteException ex) =>
        ex.SqliteErrorCode switch
        {
            5 or 6 => "the database is locked by another process",
            10 => "an I/O error occurred",
            11 => "the file is damaged",
            14 => "the file cannot be opened (permissions or missing directory)",
            26 => "the file is not a SQLite database",
            _ => $"SQLite error {ex.SqliteErrorCode}",
        };

    private static void DeleteWithSidecars(string file)
    {
        foreach (var path in new[] { file, file + "-wal", file + "-shm", file + "-journal" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A leftover temporary file is never listed or restorable.
            }
        }
    }
}
