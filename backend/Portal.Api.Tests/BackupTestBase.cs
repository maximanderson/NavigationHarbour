using Microsoft.Data.Sqlite;
using Portal.Api.Backup;
using Portal.Api.Backup.Sqlite;

namespace Portal.Api.Tests;

/// <summary>Temporary databases and backup directories for the backup tests. The real database is never used.</summary>
public abstract class BackupTestBase : IDisposable
{
    protected string Root { get; } = Path.Combine(Path.GetTempPath(), "portal-backup-" + Guid.NewGuid().ToString("N"));
    protected string LiveDir => Path.Combine(Root, "data");
    protected string LiveFile => Path.Combine(LiveDir, "usage.db");
    protected string BackupDir => Path.Combine(Root, "backups");

    protected BackupTestBase()
    {
        Directory.CreateDirectory(LiveDir);
    }

    protected SqliteBackupProvider NewProvider(string id = "portal", string? file = null) => new(id, file ?? LiveFile);

    protected BackupService NewService(TimeProvider? time = null) => new(new BackupStore(BackupDir), time, "test-1.0");

    protected static string Conn(string file, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate) =>
        new SqliteConnectionStringBuilder { DataSource = file, Mode = mode, Pooling = false }.ToString();

    /// <summary>Creates a WAL-mode Navigation Harbour style database with <paramref name="rows"/> usage rows.</summary>
    protected void CreateDatabase(string? file = null, int rows = 3)
    {
        file ??= LiveFile;
        using var c = new SqliteConnection(Conn(file));
        c.Open();
        Exec(c, "PRAGMA journal_mode = WAL;");
        Exec(c, "CREATE TABLE UsageLogs (Id INTEGER PRIMARY KEY, Title TEXT);");
        for (var i = 1; i <= rows; i++) Exec(c, $"INSERT INTO UsageLogs (Title) VALUES ('row {i}');");
    }

    /// <summary>Creates a database with <paramref name="rows"/> rows and takes a verified manual backup of it.</summary>
    protected async Task<BackupEntry> MakeBackupAsync(int rows = 3)
    {
        CreateDatabase(rows: rows);
        var entry = await NewService().CreateAsync(NewProvider(), "manual", default);
        SqliteConnection.ClearAllPools();
        return entry;
    }

    /// <summary>Adds one row to the live database and closes the connection again.</summary>
    protected void AddLiveRow()
    {
        using var c = new SqliteConnection(Conn(LiveFile));
        c.Open();
        Exec(c, "INSERT INTO UsageLogs (Title) VALUES ('later');");
        SqliteConnection.ClearAllPools();
    }

    protected static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    protected static long Rows(string file)
    {
        using var c = new SqliteConnection(Conn(file, SqliteOpenMode.ReadOnly));
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM UsageLogs;";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    protected static string Hash(string file) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}

/// <summary>A clock that returns the same instant, to force file-name collisions.</summary>
public sealed class FixedTime : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FixedTime(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
}
