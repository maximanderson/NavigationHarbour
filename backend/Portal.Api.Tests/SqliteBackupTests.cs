using Microsoft.Data.Sqlite;
using Portal.Api.Backup;

namespace Portal.Api.Tests;

/// <summary>Stage 3B.1: SQLite backup provider and the common backup orchestration.</summary>
public class SqliteBackupTests : BackupTestBase
{
    [Fact]
    public async Task Backup_IncludesCommittedDataStillInTheWal_WhileTheDatabaseIsOpen()
    {
        CreateDatabase(rows: 3);

        // A connection that stays open keeps the data in the -wal file (no checkpoint on close).
        using var live = new SqliteConnection(Conn(LiveFile));
        live.Open();
        Exec(live, "INSERT INTO UsageLogs (Title) VALUES ('in the wal');");
        Assert.True(File.Exists(LiveFile + "-wal") && new FileInfo(LiveFile + "-wal").Length > 0);

        var entry = await NewService().CreateAsync(NewProvider(), "manual", default);

        Assert.Equal(4, Rows(entry.FilePath));
    }

    [Fact]
    public async Task Backup_IsOneSelfContainedFile_WithMetadata()
    {
        CreateDatabase();

        var entry = await NewService().CreateAsync(NewProvider(), "manual", default);

        Assert.False(File.Exists(entry.FilePath + "-wal"));
        Assert.False(File.Exists(entry.FilePath + "-shm"));
        Assert.Matches(@"^portal-\d{8}T\d{6}Z\.db$", Path.GetFileName(entry.FilePath));

        var meta = entry.Metadata!;
        Assert.Equal("portal", meta.DatabaseId);
        Assert.Equal("sqlite", meta.ProviderType);
        Assert.Equal("manual", meta.Reason);
        Assert.Equal("test-1.0", meta.ApplicationVersion);
        Assert.True(meta.Verified);
        Assert.Equal(new FileInfo(entry.FilePath).Length, meta.SizeBytes);
        Assert.Equal(Hash(entry.FilePath).ToLowerInvariant(), meta.Sha256);
        Assert.True(File.Exists(entry.FilePath + BackupStore.MetaSuffix));
    }

    [Fact]
    public async Task Backup_DoesNotChangeTheSourceDatabase()
    {
        CreateDatabase();
        SqliteConnection.ClearAllPools();
        var before = Hash(LiveFile);

        await NewService().CreateAsync(NewProvider(), "manual", default);
        SqliteConnection.ClearAllPools();

        Assert.Equal(before, Hash(LiveFile));
        Assert.Equal(3, Rows(LiveFile));
    }

    [Fact]
    public async Task Backup_OfAMissingDatabase_IsRefused_AndCreatesNothing()
    {
        var ex = await Assert.ThrowsAsync<BackupSourceMissingException>(
            () => NewService().CreateAsync(NewProvider(), "manual", default));

        Assert.Contains("never creates a database", ex.Message);
        Assert.False(File.Exists(LiveFile));
        Assert.False(Directory.Exists(BackupDir));
    }

    [Fact]
    public async Task FailedBackup_LeavesNothingThatLooksLikeABackup()
    {
        // A file that is not a database makes the read-only open fail partway.
        File.WriteAllText(LiveFile, new string('x', 5000));

        await Assert.ThrowsAsync<BackupException>(() => NewService().CreateAsync(NewProvider(), "manual", default));

        Assert.Empty(Directory.GetFiles(BackupDir));
    }

    [Fact]
    public async Task BackupsTakenInTheSameSecond_GetDifferentNames()
    {
        CreateDatabase();
        var service = NewService(new FixedTime(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero)));

        var first = await service.CreateAsync(NewProvider(), "manual", default);
        var second = await service.CreateAsync(NewProvider(), "manual", default);

        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.EndsWith("portal-20261115T100000Z.db", first.FilePath);
        Assert.EndsWith("portal-20261115T100000Z-2.db", second.FilePath);
    }

    [Fact]
    public async Task Listing_ShowsNewestFirst_AndIgnoresPartialAndOtherDatabases()
    {
        CreateDatabase();
        var clock = new FixedTime(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));
        var older = await NewService(clock).CreateAsync(NewProvider(), "manual", default);
        var newer = await NewService().CreateAsync(NewProvider(), "manual", default);
        File.WriteAllText(Path.Combine(BackupDir, ".portal-x.partial"), "temp");
        File.WriteAllText(Path.Combine(BackupDir, "other-20261115T100000Z.db"), "not ours");

        var list = NewService().Store.List(NewProvider());

        Assert.Equal(new[] { newer.FilePath, older.FilePath }, list.Select(e => e.FilePath).ToArray());
    }

    [Fact]
    public async Task Listing_FlagsABackupWhoseSizeNoLongerMatches()
    {
        CreateDatabase();
        var entry = await NewService().CreateAsync(NewProvider(), "manual", default);
        File.AppendAllText(entry.FilePath, "tampered");

        var listed = NewService().Store.List(NewProvider()).Single();

        Assert.Contains("size differs", listed.Problem);
    }
}
