using Microsoft.Data.Sqlite;

namespace Portal.Api.Backup.Sqlite;

/// <summary>
/// SQLite backup provider. Everything SQLite-specific lives here: the online backup API, WAL handling,
/// integrity checks. Backup and restore use SQLite's backup API only; the live ".db", "-wal" and "-shm"
/// files are never copied or replaced by hand, so committed data in the WAL is always included and a
/// stale WAL can never be applied to a different database file.
/// Connections are not pooled, so no file handle outlives an operation.
/// </summary>
public sealed partial class SqliteBackupProvider : IDatabaseBackupProvider
{
    /// <summary>A table every Navigation Harbour database has; used to recognise a Navigation Harbour backup.</summary>
    public const string RequiredTable = "UsageLogs";

    private readonly string _filePath;

    public SqliteBackupProvider(string databaseId, string databaseFilePath)
    {
        DatabaseId = databaseId;
        _filePath = Path.GetFullPath(databaseFilePath);
    }

    public string DatabaseId { get; }
    public string ProviderType => "sqlite";
    public string BackupFileExtension => ".db";
    public string Description => _filePath;

    public IReadOnlyCollection<string> ProtectedPaths =>
        new[] { _filePath, _filePath + "-wal", _filePath + "-shm", _filePath + "-journal" };

    public bool DatabaseExists => File.Exists(_filePath);

    private static string ConnectionString(string file, SqliteOpenMode mode, int timeoutSeconds = 30) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = file,
            Mode = mode,
            Pooling = false,
            DefaultTimeout = timeoutSeconds,
        }.ToString();

    /// <summary>Consistent online snapshot of committed data, written to a new file in rollback-journal mode.</summary>
    public Task BackupAsync(string destinationFile, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            if (!DatabaseExists)
            {
                throw new BackupSourceMissingException($"The database does not exist: {_filePath}. A backup never creates a database.");
            }

            if (File.Exists(destinationFile))
            {
                throw new BackupException("The backup destination already exists; refusing to overwrite it.");
            }

            try
            {
                // The source is opened read-only: it can neither be created nor have its content changed.
                using (var source = new SqliteConnection(ConnectionString(_filePath, SqliteOpenMode.ReadOnly)))
                using (var destination = new SqliteConnection(ConnectionString(destinationFile, SqliteOpenMode.ReadWriteCreate)))
                {
                    source.Open();
                    destination.Open();
                    cancellationToken.ThrowIfCancellationRequested();

                    source.BackupDatabase(destination);

                    // A WAL-mode snapshot would need -wal/-shm files of its own. Make the backup one self-contained file.
                    Scalar(destination, "PRAGMA journal_mode = DELETE;");
                }
            }
            catch (SqliteException ex)
            {
                DeleteWithSidecars(destinationFile);
                throw new BackupException($"SQLite backup failed: {Describe(ex)}", ex);
            }
            catch
            {
                DeleteWithSidecars(destinationFile);
                throw;
            }
            finally
            {
                SqliteConnection.ClearAllPools();
            }
        }, cancellationToken);

    internal static string? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
