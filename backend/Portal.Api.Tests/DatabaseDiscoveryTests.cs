using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Portal.Api.Configuration.Server;
using Portal.Api.Persistence;

namespace Portal.Api.Tests;

/// <summary>
/// Default database discovery: usage.db for new installations, an existing legacy portalcore.db keeps being used,
/// both existing blocks startup, and an explicit location is never second-guessed. Nothing is renamed or created.
/// </summary>
public class DatabaseDiscoveryTests : ApplyTestBase
{
    private string Current => Path.Combine(Home, "data", DatabaseLocation.FileName);
    private string Legacy => Path.Combine(Home, "data", DatabaseLocation.LegacyFileName);

    private static void MakeDatabase(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE T (Id INTEGER); INSERT INTO T VALUES (42);";
        command.ExecuteNonQuery();
        SqliteConnection.ClearAllPools();
    }

    private DatabaseLocation Resolve(string environment = "Production")
    {
        WriteIni("[paths]\ndata = data\n");
        return Load(WithShippedDefaults(), environment).Database!;
    }

    [Fact]
    public void DefaultNames_AreTheDocumentedOnes()
    {
        Assert.Equal("usage.db", DatabaseLocation.FileName);
        Assert.Equal("portalcore.db", DatabaseLocation.LegacyFileName);
    }

    [Fact]
    public void OnlyCurrent_IsUsed()
    {
        MakeDatabase(Current);

        var db = Resolve();

        Assert.Equal(Current, db.FilePath);
        Assert.Equal(DatabaseState.Exists, db.State);
        Assert.Null(db.Blocker);
    }

    [Fact]
    public void OnlyLegacy_KeepsBeingUsed_AndIsNotRenamed()
    {
        MakeDatabase(Legacy);

        var db = Resolve();

        Assert.Equal(Legacy, db.FilePath);
        Assert.Equal(DatabaseState.Exists, db.State);
        Assert.Null(db.Blocker);
        Assert.Contains("legacy", db.Origin);
        Assert.Contains(Legacy, db.RuntimeConnectionString);
        Assert.True(File.Exists(Legacy));
        Assert.False(File.Exists(Current));
    }

    [Fact]
    public void Neither_ResolvesToCurrent_AndFollowsTheInitializationPolicy()
    {
        Directory.CreateDirectory(Path.Combine(Home, "data"));

        var production = Resolve();
        Assert.Equal(Current, production.FilePath);
        Assert.Equal(DatabaseState.Missing, production.State);
        Assert.NotNull(production.Blocker);
        Assert.True(production.CanInitialize);
        Assert.False(File.Exists(Current));
    }

    [Fact]
    public void Neither_InDevelopmentDefaultLocation_MayBeCreatedAsCurrent()
    {
        var db = Load(WithShippedDefaults(), "Development").Database!;

        Assert.Equal(Path.Combine(Home, DatabaseLocation.FileName), db.FilePath);
        Assert.True(db.CreateOnStartup);
    }

    [Fact]
    public void Development_WithOnlyLegacyAtDefaultLocation_UsesItAndCreatesNothing()
    {
        var legacy = Path.Combine(Home, DatabaseLocation.LegacyFileName);
        MakeDatabase(legacy);

        var db = Load(WithShippedDefaults(), "Development").Database!;

        Assert.Equal(legacy, db.FilePath);
        Assert.False(db.CreateOnStartup);
        Assert.Null(db.Blocker);
    }

    [Fact]
    public void Both_BlocksStartup_WithAClearMessage_AndChangesNothing()
    {
        MakeDatabase(Current);
        MakeDatabase(Legacy);
        var before = (File.ReadAllBytes(Current), File.ReadAllBytes(Legacy));

        var db = Resolve();

        Assert.Equal(DatabaseState.Ambiguous, db.State);
        Assert.NotNull(db.Blocker);
        Assert.Contains(Current, db.Blocker);
        Assert.Contains(Legacy, db.Blocker);
        Assert.Contains("explicitly", db.Blocker);
        Assert.False(db.CanInitialize);
        Assert.Equal(before.Item1, File.ReadAllBytes(Current));
        Assert.Equal(before.Item2, File.ReadAllBytes(Legacy));
    }

    [Fact]
    public void Both_IsAlsoReportedByCheckConfig()
    {
        MakeDatabase(Current);
        MakeDatabase(Legacy);
        WriteIni("[paths]\ndata = data\n");
        var report = Load(WithShippedDefaults());
        var output = new StringWriter();

        var exit = ServerConfigCommands.Run(StartupOptions.Parse(new[] { "--check-config" }), report, output);

        Assert.NotEqual(0, exit);
        Assert.Contains("Database:", output.ToString());
    }

    [Fact]
    public void Both_InitDbRefuses_AndCreatesNothing()
    {
        MakeDatabase(Current);
        MakeDatabase(Legacy);
        var db = Resolve();
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = DatabaseInitializer.Run(db, output, error);

        Assert.Equal(2, exit);
        Assert.Contains("Nothing was changed", error.ToString());
    }

    [Fact]
    public void OnlyLegacy_InitDb_DoesNotCreateAnEmptyCurrentDatabase()
    {
        MakeDatabase(Legacy);
        var db = Resolve();
        var output = new StringWriter();

        var exit = DatabaseInitializer.Run(db, output, new StringWriter());

        Assert.Equal(0, exit);
        Assert.Contains("already exists", output.ToString());
        Assert.False(File.Exists(Current));
    }

    [Fact]
    public void ExplicitConnectionString_IsNeverSecondGuessed()
    {
        // usage.db and portalcore.db both exist, but the location was chosen on purpose.
        MakeDatabase(Current);
        MakeDatabase(Legacy);
        var config = WithShippedDefaults();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ServerConfiguration.ConnectionStringKey] = $"Data Source={Legacy}",
        });
        WriteIni("[server]\n");

        var db = Load(config).Database!;

        Assert.Equal(Legacy, db.FilePath);
        Assert.Equal(DatabaseState.Exists, db.State);
        Assert.Null(db.Blocker);
    }

    [Fact]
    public void ExplicitConnectionString_EqualToTheIniDerivedOne_IsStillExplicit()
    {
        // The documented way out of "both exist": name usage.db in the connection string. The value is identical
        // to what [paths] data = data produces, so only the providing source can tell it is explicit.
        MakeDatabase(Current);
        MakeDatabase(Legacy);
        WriteIni("[paths]\ndata = data\n");
        var config = WithShippedDefaults();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ServerConfiguration.ConnectionStringKey] = "Data Source=" + Path.Combine(Home, "data", DatabaseLocation.FileName),
        });

        var db = Load(config).Database!;

        Assert.Equal(Current, db.FilePath);
        Assert.Equal(DatabaseState.Exists, db.State);
        Assert.Null(db.Blocker);
        Assert.True(db.IsExplicit);
    }

    [Fact]
    public void ExplicitConnectionString_WithCurrentName_IgnoresALegacyNeighbour()
    {
        MakeDatabase(Current);
        MakeDatabase(Legacy);
        var config = WithShippedDefaults();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ServerConfiguration.ConnectionStringKey] = $"Data Source={Current}",
        });
        WriteIni("[server]\n");

        var db = Load(config).Database!;

        Assert.Equal(Current, db.FilePath);
        Assert.Null(db.Blocker);
    }
}
