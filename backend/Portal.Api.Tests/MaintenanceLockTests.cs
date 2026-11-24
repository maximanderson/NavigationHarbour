using Portal.Api.Backup;

namespace Portal.Api.Tests;

/// <summary>Stage 3B.1: the cross-process maintenance lock; the lock holder is a separate OS process.</summary>
public class MaintenanceLockTests : BackupTestBase
{
    private string[] Files() =>
        Directory.GetFiles(LiveDir).Concat(Directory.GetFiles(BackupDir))
            .Where(f => !f.EndsWith(MaintenanceLock.FileSuffix)).Order().ToArray();

    [Fact]
    public async Task Restore_IsRefused_WhileAServerProcessHoldsTheLock_AndChangesNothing()
    {
        var entry = await MakeBackupAsync(rows: 3);
        AddLiveRow();
        var hashBefore = Hash(LiveFile);
        var filesBefore = Files();

        using var server = LockHolderProcess.Start("shared", LiveFile);
        Assert.True(server.HoldsLock, server.FirstLine);

        var ex = await Assert.ThrowsAsync<MaintenanceLockBusyException>(
            () => NewService().RestoreAsync(NewProvider(), entry.FilePath, default));

        Assert.Contains("held by another process", ex.Message);
        // File list first: reading a WAL database (Rows) itself creates -wal/-shm files.
        Assert.Equal(filesBefore, Files());
        Assert.Equal(hashBefore, Hash(LiveFile));
        Assert.Equal(4, Rows(LiveFile));
    }

    [Fact]
    public async Task RestoreCheck_WithoutYes_IsAlsoRefused_WhileAServerProcessHoldsTheLock()
    {
        var entry = await MakeBackupAsync();
        using var server = LockHolderProcess.Start("shared", LiveFile);
        Assert.True(server.HoldsLock, server.FirstLine);

        await Assert.ThrowsAsync<MaintenanceLockBusyException>(
            () => NewService().CheckRestoreAsync(NewProvider(), entry.FilePath, default));
    }

    [Fact]
    public async Task Restore_Succeeds_AfterTheServerProcessReleasesTheLock()
    {
        var entry = await MakeBackupAsync(rows: 3);
        AddLiveRow();

        using var server = LockHolderProcess.Start("shared", LiveFile);
        Assert.True(server.HoldsLock, server.FirstLine);
        await Assert.ThrowsAsync<MaintenanceLockBusyException>(
            () => NewService().RestoreAsync(NewProvider(), entry.FilePath, default));

        server.ReleaseGracefully();
        var result = await NewService().RestoreAsync(NewProvider(), entry.FilePath, default);

        Assert.Equal(3, Rows(LiveFile));
        Assert.Equal(4, Rows(result.SafetyBackup!.FilePath));
    }

    [Fact]
    public async Task Restore_Succeeds_AfterTheServerProcessWasKilled()
    {
        var entry = await MakeBackupAsync(rows: 3);
        AddLiveRow();

        using var server = LockHolderProcess.Start("shared", LiveFile);
        Assert.True(server.HoldsLock, server.FirstLine);
        server.Kill();

        await NewService().RestoreAsync(NewProvider(), entry.FilePath, default);

        Assert.Equal(3, Rows(LiveFile));
    }

    [Fact]
    public async Task Restore_Succeeds_AfterTheServerProcessCrashed_AndALeftoverLockFileIsHarmless()
    {
        var entry = await MakeBackupAsync(rows: 3);
        AddLiveRow();

        using var server = LockHolderProcess.Start("shared", LiveFile, "crash");
        Assert.True(server.HoldsLock, server.FirstLine);
        server.WaitForExit();

        Assert.True(File.Exists(MaintenanceLock.PathFor(LiveFile)), "the lock file stays behind after a crash");
        await NewService().RestoreAsync(NewProvider(), entry.FilePath, default);

        Assert.Equal(3, Rows(LiveFile));
    }

    [Fact]
    public void ServerProcess_CannotStart_WhileARestoreHoldsTheLock()
    {
        CreateDatabase();
        using var restoreLock = NewProvider().AcquireMaintenanceLock(createDirectory: true);

        using var server = LockHolderProcess.Start("shared", LiveFile);

        Assert.False(server.HoldsLock);
        Assert.StartsWith("BUSY", server.FirstLine);
    }

    [Fact]
    public void SeveralServerProcesses_CanShareTheLock_AndARestoreWaitsForAll()
    {
        CreateDatabase();

        using var first = LockHolderProcess.Start("shared", LiveFile);
        using var second = LockHolderProcess.Start("shared", LiveFile);
        Assert.True(first.HoldsLock, first.FirstLine);
        Assert.True(second.HoldsLock, second.FirstLine);

        first.ReleaseGracefully();
        Assert.Throws<MaintenanceLockBusyException>(() => MaintenanceLock.AcquireExclusive(LiveFile));

        second.ReleaseGracefully();
        using var exclusive = MaintenanceLock.AcquireExclusive(LiveFile);
        Assert.True(exclusive.IsExclusive);
        Assert.True(exclusive.IsEnforced);
    }

    [Fact]
    public void TwoRestores_CannotRunAtTheSameTime_AcrossProcesses()
    {
        CreateDatabase();

        using var other = LockHolderProcess.Start("exclusive", LiveFile);
        Assert.True(other.HoldsLock, other.FirstLine);

        Assert.Throws<MaintenanceLockBusyException>(() => NewProvider().AcquireMaintenanceLock(createDirectory: true));
    }

    [Fact]
    public void TheLock_BelongsToTheDatabaseLocation_NotToTheWorkingDirectory()
    {
        var otherFile = Path.Combine(Root, "other-data", "usage.db");
        Directory.CreateDirectory(Path.GetDirectoryName(otherFile)!);
        CreateDatabase();

        using var server = LockHolderProcess.Start("shared", LiveFile);
        Assert.True(server.HoldsLock, server.FirstLine);

        // A different database is not affected; the same database spelled differently is.
        using var elsewhere = MaintenanceLock.AcquireExclusive(otherFile);
        Assert.Equal(otherFile + MaintenanceLock.FileSuffix, elsewhere.FilePath);

        var spelledDifferently = Path.Combine(LiveDir, "..", "data", "usage.db");
        Assert.Throws<MaintenanceLockBusyException>(() => MaintenanceLock.AcquireExclusive(spelledDifferently));
        Assert.Equal(Path.GetFullPath(LiveFile) + MaintenanceLock.FileSuffix, MaintenanceLock.PathFor(spelledDifferently));
    }
}
