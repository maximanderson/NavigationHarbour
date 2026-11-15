using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using PortalCore.Api.Configuration.Server;
using PortalCore.Api.Persistence;

namespace PortalCore.Api.Tests;

/// <summary>Stage 3A: odd files, conflicting settings and --init-db.</summary>
public class DatabaseInitAndConflictTests : ApplyTestBase
{
    private string DbFile => Path.Combine(Home, "data", "portalcore.db");

    private DatabaseLocation Resolve(string ini = "[paths]\ndata = data\n")
    {
        WriteIni(ini);
        return Load(WithShippedDefaults()).Database!;
    }

    [Fact]
    public void NonSqliteFile_IsABlocker_AndLeftUntouched()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbFile)!);
        File.WriteAllText(DbFile, "this is not a database, just some text that is long enough");
        var before = File.ReadAllBytes(DbFile);

        var db = Resolve();

        Assert.Equal(DatabaseState.NotSqlite, db.State);
        Assert.Contains("not a SQLite database", db.Blocker);
        Assert.Equal(before, File.ReadAllBytes(DbFile));
    }

    [Fact]
    public void EmptyFile_IsABlocker_ButInitializable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbFile)!);
        File.WriteAllBytes(DbFile, Array.Empty<byte>());

        var db = Resolve();

        Assert.Equal(DatabaseState.EmptyFile, db.State);
        Assert.NotNull(db.Blocker);
        Assert.True(db.CanInitialize);
    }

    [Fact]
    public void ConnectionStringFromCommandLine_WinsOverServerIni_WithAWarning()
    {
        var other = Path.Combine(Home, "other", "x.db");
        WriteIni("[paths]\ndata = data\n");
        var config = WithShippedDefaults();
        config.AddCommandLine(new[] { $"--ConnectionStrings:PortalDb=Data Source={other}" });

        var report = Load(config);

        Assert.Equal(other, report.Database!.FilePath);
        Assert.True(report.Database.IsExplicit);
        Assert.Contains(report.Warnings, w => w.Contains("ConnectionStrings:PortalDb") && w.Contains("[paths] data is ignored"));
    }

    [Fact]
    public void ConnectionStringSecrets_AreNeverEchoed()
    {
        var config = WithShippedDefaults();
        config.AddCommandLine(new[] { "--ConnectionStrings:PortalDb=Data Source=/x/y.db;Password=hunter2" });

        var report = Load(config);
        var text = string.Join("\n", report.Warnings.Concat(report.Errors));

        Assert.DoesNotContain("hunter2", text);
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=file:x.db?mode=memory")]
    [InlineData("Mode=ReadWriteCreate;Data Source=x.db")]
    [InlineData("Cache=Shared")]
    public void UnsupportedConnectionStrings_AreRejected(string value)
    {
        var config = WithShippedDefaults();
        config.AddCommandLine(new[] { $"--ConnectionStrings:PortalDb={value}" });

        var report = Load(config);

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.StartsWith("[database]"));
    }

    [Fact]
    public void InitDb_CreatesANewDatabase_ThenLeavesItAlone()
    {
        Directory.CreateDirectory(Path.Combine(Home, "data"));
        var db = Resolve();
        var output = new StringWriter();

        Assert.Equal(0, DatabaseInitializer.Run(db, output, new StringWriter()));
        Assert.True(File.Exists(DbFile));

        SqliteConnection.ClearAllPools();
        var before = File.ReadAllBytes(DbFile);
        var again = Load(WithShippedDefaults()).Database!;
        Assert.Equal(DatabaseState.Exists, again.State);

        var second = new StringWriter();
        Assert.Equal(0, DatabaseInitializer.Run(again, second, new StringWriter()));
        Assert.Contains("already exists", second.ToString());
        Assert.Equal(before, File.ReadAllBytes(DbFile));
    }

    [Fact]
    public void InitDb_CreatesOnlyTheLastDirectoryLevel()
    {
        var db = Resolve("[paths]\ndata = a/b/data\n");
        var error = new StringWriter();

        Assert.Equal(2, DatabaseInitializer.Run(db, new StringWriter(), error));
        Assert.False(Directory.Exists(Path.Combine(Home, "a")));
        Assert.Contains("parent directory", error.ToString());
    }

    [Fact]
    public void InitDb_RefusesANonSqliteFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbFile)!);
        File.WriteAllText(DbFile, "not sqlite, but not empty either");

        var db = Resolve();

        Assert.Equal(2, DatabaseInitializer.Run(db, new StringWriter(), new StringWriter()));
        Assert.Equal("not sqlite, but not empty either", File.ReadAllText(DbFile));
    }

    [Fact]
    public void RuntimeConnection_CannotCreateAMissingFile()
    {
        Directory.CreateDirectory(Path.Combine(Home, "data"));
        var db = Resolve();

        using var connection = new SqliteConnection(db.RuntimeConnectionString);
        Assert.Throws<SqliteException>(() => connection.Open());
        Assert.False(File.Exists(DbFile));
    }
}
