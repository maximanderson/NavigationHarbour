using Microsoft.Data.Sqlite;
using PortalCore.Api.Backup;

namespace PortalCore.Api.Tests;

/// <summary>Stage 3B.1: restore safety.</summary>
public class RestoreTests : BackupTestBase
{
    private async Task<BackupEntry> MakeBackup(int rows = 3)
    {
        CreateDatabase(rows: rows);
        var entry = await NewService().CreateAsync(NewProvider(), "manual", default);
        SqliteConnection.ClearAllPools();
        return entry;
    }

    [Fact]
    public async Task Restore_ReplacesTheDatabase_AfterASafetyBackup()
    {
        var entry = await MakeBackup(rows: 3);
        using (var c = new SqliteConnection(Conn(LiveFile)))
        {
            c.Open();
            Exec(c, "INSERT INTO UsageLogs (Title) VALUES ('added later');");
        }
        SqliteConnection.ClearAllPools();
        Assert.Equal(4, Rows(LiveFile));

        var result = await NewService().RestoreAsync(NewProvider(), entry.FilePath, default);
        SqliteConnection.ClearAllPools();

        Assert.Equal(3, Rows(LiveFile));
        Assert.NotNull(result.SafetyBackup);
        Assert.Equal("pre-restore", result.SafetyBackup!.Metadata!.Reason);
        Assert.Equal(4, Rows(result.SafetyBackup.FilePath));
        Assert.Equal(3, Rows(entry.FilePath));
    }

    [Fact]
    public async Task Restore_IntoAMissingDatabase_RecreatesIt_WithoutASafetyBackup()
    {
        var entry = await MakeBackup(rows: 5);
        foreach (var f in new[] { "", "-wal", "-shm" }) if (File.Exists(LiveFile + f)) File.Delete(LiveFile + f);

        var result = await NewService().RestoreAsync(NewProvider(), entry.FilePath, default);
        SqliteConnection.ClearAllPools();

        Assert.Null(result.SafetyBackup);
        Assert.Equal(5, Rows(LiveFile));
    }

    // The "database in use" protection is the cross-process maintenance lock (MaintenanceLockTests).
    // An idle SQLite connection of an external client is deliberately NOT detected; that is documented.
    [Fact]
    public async Task Restore_DoesNotDependOnSqlConnections_ItsOwnConnectionsAreAllClosedBeforeTheSwap()
    {
        var entry = await MakeBackup(rows: 3);
        var service = NewService();

        await service.RestoreAsync(NewProvider(), entry.FilePath, default);

        // If a connection of the restore process were still open, the files could not be removed on Windows
        // and a pooled handle would show up here. Opening and deleting must work right away.
        Assert.Equal(3, Rows(LiveFile));
        Assert.False(File.Exists(LiveFile + "-wal") && new FileInfo(LiveFile + "-wal").Length > 0);
        Assert.Empty(Directory.GetFiles(LiveDir, "*.restore-*"));
    }

    [Fact]
    public async Task Restore_NeverOverwritesAFileThatIsNotSqlite()
    {
        var entry = await MakeBackup();
        foreach (var f in new[] { "", "-wal", "-shm" }) if (File.Exists(LiveFile + f)) File.Delete(LiveFile + f);
        File.WriteAllText(LiveFile, "precious text that is not a database " + new string('.', 200));

        await Assert.ThrowsAsync<BackupException>(() => NewService().RestoreAsync(NewProvider(), entry.FilePath, default));

        Assert.StartsWith("precious text", File.ReadAllText(LiveFile));
    }

    [Fact]
    public async Task Restore_RefusesACorruptBackup_AndLeavesTheLiveDatabaseAlone()
    {
        var entry = await MakeBackup(rows: 3);
        File.AppendAllText(entry.FilePath, "damage");
        var before = Hash(LiveFile);

        await Assert.ThrowsAsync<BackupException>(() => NewService().RestoreAsync(NewProvider(), entry.FilePath, default));
        SqliteConnection.ClearAllPools();

        Assert.Equal(before, Hash(LiveFile));
    }

    [Fact]
    public async Task Restore_CannotUseTheLiveDatabaseOrAPartialFileAsASource()
    {
        CreateDatabase();
        SqliteConnection.ClearAllPools();

        await Assert.ThrowsAsync<BackupException>(() => NewService().RestoreAsync(NewProvider(), LiveFile, default));

        Directory.CreateDirectory(BackupDir);
        var partial = Path.Combine(BackupDir, ".portal-x.db.1234.partial");
        File.WriteAllText(partial, "x");
        await Assert.ThrowsAsync<BackupException>(() => NewService().RestoreAsync(NewProvider(), partial, default));
    }
}
