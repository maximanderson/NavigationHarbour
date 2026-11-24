using Microsoft.Data.Sqlite;
using Portal.Api.Backup;

namespace Portal.Api.Tests;

/// <summary>Stage 3B.1: what a restore does with the lock held, and what it leaves behind when it fails.</summary>
public class RestoreSwapTests : BackupTestBase
{
    [Fact]
    public async Task Restore_ProtectsTheWholeOperation_TheLockIsHeldWhileTheSafetyBackupIsTaken()
    {
        var entry = await MakeBackupAsync(rows: 3);
        var provider = new LockProbingProvider(NewProvider(), LiveFile);

        await NewService().RestoreAsync(provider, entry.FilePath, default);

        Assert.True(provider.LockWasHeldDuringBackup, "the safety backup ran without the lock");
        Assert.True(provider.LockWasHeldDuringRestore, "the replacement ran without the lock");
        Assert.False(provider.IsLockedAfterwards(), "the lock was not released after the restore");
    }

    [Fact]
    public async Task Restore_FailingDuringTheSwap_PutsTheOriginalDatabaseBack()
    {
        var entry = await MakeBackupAsync(rows: 3);
        AddLiveRow();
        var hashBefore = Hash(LiveFile);
        var provider = NewProvider();
        provider.FaultAfterOriginalsMovedAside = () => throw new IOException("simulated failure after the originals were moved aside");

        var ex = await Assert.ThrowsAsync<BackupException>(() => NewService().RestoreAsync(provider, entry.FilePath, default));

        Assert.Contains("original database was put back", ex.Message);
        Assert.Equal(hashBefore, Hash(LiveFile));
        Assert.Equal(4, Rows(LiveFile));
        Assert.Empty(Directory.GetFiles(LiveDir, "*.restore-*"));
        Assert.Single(NewService().Store.List(NewProvider()), e => e.Metadata?.Reason == "pre-restore");
    }

    [Fact]
    public async Task Restore_IsRefused_AfterAnInterruptedRestoreLeftTheOriginalAside()
    {
        var entry = await MakeBackupAsync(rows: 3);
        File.Copy(LiveFile, LiveFile + ".restore-aside-deadbeef");
        var hashBefore = Hash(LiveFile);

        var ex = await Assert.ThrowsAsync<BackupException>(() => NewService().RestoreAsync(NewProvider(), entry.FilePath, default));

        Assert.Contains("interrupted", ex.Message);
        Assert.Equal(hashBefore, Hash(LiveFile));
        Assert.Single(NewService().Store.List(NewProvider()));
    }

    [Fact]
    public async Task Restore_RemovesLeftoverPartialFilesOfACrashedAttempt()
    {
        var entry = await MakeBackupAsync(rows: 3);
        File.WriteAllText(LiveFile + ".restore-12345678.partial", "junk");

        await NewService().RestoreAsync(NewProvider(), entry.FilePath, default);

        Assert.Empty(Directory.GetFiles(LiveDir, "*.restore-*"));
        Assert.Equal(3, Rows(LiveFile));
    }

    [Fact]
    public async Task Restore_KeepsWalMode_AndLeavesNoStaleWalContent()
    {
        var entry = await MakeBackupAsync(rows: 3);
        AddLiveRow();

        await NewService().RestoreAsync(NewProvider(), entry.FilePath, default);

        using (var c = new SqliteConnection(Conn(LiveFile)))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode;";
            Assert.Equal("wal", Convert.ToString(cmd.ExecuteScalar()));
        }

        SqliteConnection.ClearAllPools();
        Assert.Equal(3, Rows(LiveFile));
        Assert.False(File.Exists(LiveFile + "-wal") && new FileInfo(LiveFile + "-wal").Length > 0);
    }

    [Fact]
    public async Task Restore_ClosesAllItsConnections_SoNoRestoreFilesOrHandlesRemain()
    {
        var entry = await MakeBackupAsync(rows: 3);

        await NewService().RestoreAsync(NewProvider(), entry.FilePath, default);

        Assert.Empty(Directory.GetFiles(LiveDir, "*.restore-*"));
        Assert.Equal(3, Rows(LiveFile));
    }
}
