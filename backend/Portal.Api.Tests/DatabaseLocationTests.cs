using Microsoft.Data.Sqlite;
using Portal.Api.Configuration.Server;
using Portal.Api.Persistence;

namespace Portal.Api.Tests;

/// <summary>Stage 3A: one effective database location, resolved without opening the file.</summary>
public class DatabaseLocationTests : ApplyTestBase
{
    private string DbFile(string dir = "data") => Path.Combine(Home, dir, "usage.db");

    /// <summary>Creates a real SQLite file (with a table and a row) in a temporary location.</summary>
    private void MakeDatabase(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE T (Id INTEGER); INSERT INTO T VALUES (42);";
        command.ExecuteNonQuery();
        SqliteConnection.ClearAllPools();
    }

    private static string Checksum(string path) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    [Fact]
    public void DataPath_IsAppliedAsHomeRelativeDatabaseFile()
    {
        WriteIni("[paths]\ndata = data\n");
        MakeDatabase(DbFile());

        var db = Load(WithShippedDefaults()).Database!;

        Assert.Equal(DbFile(), db.FilePath);
        Assert.Equal(DatabaseState.Exists, db.State);
        Assert.Null(db.Blocker);
        Assert.Contains("server.ini", db.Origin);
        Assert.Contains("Mode=ReadWrite", db.RuntimeConnectionString);
        Assert.DoesNotContain("ReadWriteCreate", db.RuntimeConnectionString);
    }

    [Fact]
    public void AbsoluteDataPath_IsUsedAsWritten()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pc-abs-data-" + Guid.NewGuid().ToString("N"));
        WriteIni($"[paths]\ndata = {dir}\n");

        var db = Load(WithShippedDefaults()).Database!;

        Assert.Equal(Path.Combine(dir, "usage.db"), db.FilePath);
        Assert.Equal(DatabaseState.DirectoryMissing, db.State);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void WorkingDirectory_DoesNotChangeTheLocation()
    {
        WriteIni("[paths]\ndata = data\n");
        var original = Directory.GetCurrentDirectory();
        try
        {
            var first = Load(WithShippedDefaults()).Database!.FilePath;
            Directory.SetCurrentDirectory(Path.GetTempPath());
            var second = Load(WithShippedDefaults()).Database!.FilePath;

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public void ExistingDatabase_IsNeverModifiedByResolutionOrDiagnostics()
    {
        WriteIni("[paths]\ndata = data\n");
        MakeDatabase(DbFile());
        var before = Checksum(DbFile());
        var files = Directory.GetFiles(Path.GetDirectoryName(DbFile())!).Length;

        var report = Load(WithShippedDefaults());
        ServerConfigCommands.Run(StartupOptions.Parse(new[] { "--print-config", "--check-config" }), report, new StringWriter());

        Assert.Equal(before, Checksum(DbFile()));
        Assert.Equal(files, Directory.GetFiles(Path.GetDirectoryName(DbFile())!).Length);
    }

    [Fact]
    public void MissingDatabase_InProduction_IsABlocker_AndNothingIsCreated()
    {
        WriteIni("[paths]\ndata = data\n");
        Directory.CreateDirectory(Path.Combine(Home, "data"));

        var db = Load(WithShippedDefaults()).Database!;

        Assert.Equal(DatabaseState.Missing, db.State);
        Assert.NotNull(db.Blocker);
        Assert.Contains(DbFile(), db.Blocker);
        Assert.Contains("disabled", db.Blocker);
        Assert.False(db.CreateOnStartup);
        Assert.False(File.Exists(DbFile()));
    }

    [Fact]
    public void MissingDatabase_InDevelopment_AtDefaultLocation_MayBeCreated()
    {
        var db = Load(WithShippedDefaults(), "Development").Database!;

        Assert.Equal(Path.Combine(Home, "usage.db"), db.FilePath);
        Assert.Equal(DatabaseState.Missing, db.State);
        Assert.False(db.IsExplicit);
        Assert.True(db.CreateOnStartup);
        Assert.Null(db.Blocker);
    }

    [Fact]
    public void MissingDatabase_InDevelopment_AtExplicitLocation_IsABlocker()
    {
        WriteIni("[paths]\ndata = data\n");
        Directory.CreateDirectory(Path.Combine(Home, "data"));

        var db = Load(WithShippedDefaults(), "Development").Database!;

        Assert.True(db.IsExplicit);
        Assert.False(db.CreateOnStartup);
        Assert.Contains("explicitly", db.Blocker);
    }
}
