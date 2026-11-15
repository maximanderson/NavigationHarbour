using Microsoft.Data.Sqlite;
using PortalCore.Api.Backup;

namespace PortalCore.Api.Tests;

/// <summary>Stage 3B.1: backup verification.</summary>
public class VerifyTests : BackupTestBase
{
    private async Task<BackupEntry> MakeBackup(int rows = 3)
    {
        CreateDatabase(rows: rows);
        var entry = await NewService().CreateAsync(NewProvider(), "manual", default);
        SqliteConnection.ClearAllPools();
        return entry;
    }

    [Fact]
    public async Task Verify_AcceptsAGoodBackup_AndChecksTheChecksum()
    {
        var entry = await MakeBackup();

        var result = await NewService().VerifyAsync(NewProvider(), entry.FilePath, default);

        Assert.True(result.ChecksumChecked);
    }

    [Fact]
    public async Task Verify_FindsAPlainFileNameInTheBackupDirectory()
    {
        var entry = await MakeBackup();

        var result = await NewService().VerifyAsync(NewProvider(), Path.GetFileName(entry.FilePath), default);

        Assert.True(result.ChecksumChecked);
    }

    [Fact]
    public async Task Verify_RejectsACorruptedBackup()
    {
        var entry = await MakeBackup();
        var bytes = File.ReadAllBytes(entry.FilePath);
        bytes[bytes.Length / 2] ^= 0xFF;
        File.WriteAllBytes(entry.FilePath, bytes);

        var ex = await Assert.ThrowsAsync<BackupException>(() => NewService().VerifyAsync(NewProvider(), entry.FilePath, default));

        Assert.Contains("checksum", ex.Message);
    }

    [Fact]
    public async Task Verify_RejectsAFileThatIsNotSqlite_EvenWithoutMetadata()
    {
        Directory.CreateDirectory(BackupDir);
        var bogus = Path.Combine(BackupDir, "portal-20260101T000000Z.db");
        File.WriteAllText(bogus, new string('z', 4096));

        await Assert.ThrowsAsync<BackupException>(() => NewService().VerifyAsync(NewProvider(), bogus, default));
    }

    [Fact]
    public async Task Verify_RejectsASqliteFileThatIsNotAPortalCoreDatabase()
    {
        Directory.CreateDirectory(BackupDir);
        var other = Path.Combine(BackupDir, "other.db");
        using (var c = new SqliteConnection(Conn(other)))
        {
            c.Open();
            Exec(c, "CREATE TABLE Something (Id INTEGER);");
        }
        SqliteConnection.ClearAllPools();

        var ex = await Assert.ThrowsAsync<BackupException>(() => NewService().VerifyAsync(NewProvider(), other, default));

        Assert.Contains("not a Portal Core database", ex.Message);
    }

    [Fact]
    public async Task Verify_RejectsABackupOfAnotherDatabaseId()
    {
        var entry = await MakeBackup();

        var ex = await Assert.ThrowsAsync<BackupException>(
            () => NewService().VerifyAsync(NewProvider("reports"), entry.FilePath, default));

        Assert.Contains("belongs to database \"portal\"", ex.Message);
    }
}
