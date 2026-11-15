using PortalCore.Api.Backup;
using PortalCore.Api.Configuration.Server;

namespace PortalCore.Api.Tests;

/// <summary>Stage 3B.1: registry, directory safety, command parsing and configuration.</summary>
public class BackupFrameworkTests : ApplyTestBase
{
    private sealed class FakeProvider : IDatabaseBackupProvider
    {
        public FakeProvider(string id) => DatabaseId = id;
        public string DatabaseId { get; }
        public string ProviderType => "fake";
        public string BackupFileExtension => ".bak";
        public string Description => "fake";
        public IReadOnlyCollection<string> ProtectedPaths => Array.Empty<string>();
        public bool DatabaseExists => true;
        public IDisposable AcquireMaintenanceLock(bool createDirectory) => new Release();
        private sealed class Release : IDisposable { public void Dispose() { } }
        public Task EnsureRestoreAllowedAsync(CancellationToken c) => Task.CompletedTask;
        public Task BackupAsync(string d, CancellationToken c) { File.WriteAllText(d, "data"); return Task.CompletedTask; }
        public Task VerifyAsync(string f, CancellationToken c) => Task.CompletedTask;
        public Task RestoreAsync(string f, CancellationToken c) => Task.CompletedTask;
    }

    [Fact]
    public void Registry_SelectsTheOnlyProviderByDefault_AndOthersByIdentifier()
    {
        var one = new BackupRegistry(new[] { new FakeProvider("portal") });
        Assert.Equal("portal", one.Resolve(null).DatabaseId);
        Assert.Equal("portal", one.Resolve("PORTAL").DatabaseId);

        var two = new BackupRegistry(new[] { new FakeProvider("portal"), new FakeProvider("reports") });
        Assert.Contains("--database", Assert.Throws<BackupException>(() => two.Resolve(null)).Message);
        Assert.Equal("reports", two.Resolve("reports").DatabaseId);
        Assert.Contains("Unknown database", Assert.Throws<BackupException>(() => two.Resolve("nope")).Message);
    }

    [Theory]
    [InlineData("Portal")]
    [InlineData("a b")]
    [InlineData("../x")]
    [InlineData("")]
    public void Registry_RejectsUnsafeIdentifiers(string id)
    {
        Assert.Throws<ArgumentException>(() => new BackupRegistry(new[] { new FakeProvider(id) }));
    }

    [Fact]
    public void Registry_RejectsDuplicateIdentifiers()
    {
        Assert.Throws<ArgumentException>(() => new BackupRegistry(new[] { new FakeProvider("portal"), new FakeProvider("PORTAL".ToLowerInvariant()) }));
    }

    [Fact]
    public async Task TheCommonFramework_WorksWithAnyProvider()
    {
        var service = new BackupService(new BackupStore(Path.Combine(Home, "bk")), null, "t");

        var entry = await service.CreateAsync(new FakeProvider("other"), "manual", default);

        Assert.EndsWith(".bak", entry.FilePath);
        Assert.Equal("fake", entry.Metadata!.ProviderType);
        Assert.Single(service.Store.List(new FakeProvider("other")));
    }

    [Fact]
    public void BackupDirectory_MayNotBeOrContainTheDatabaseDirectory()
    {
        var provider = new PortalCore.Api.Backup.Sqlite.SqliteBackupProvider("portal", Path.Combine(Home, "data", "portalcore.db"));

        Assert.Throws<BackupException>(() => new BackupStore(Path.Combine(Home, "data")).EnsureSafeFor(provider));
        Assert.Throws<BackupException>(() => new BackupStore(Home).EnsureSafeFor(provider));
        new BackupStore(Path.Combine(Home, "backups")).EnsureSafeFor(provider);
        new BackupStore(Path.Combine(Home, "data", "backups")).EnsureSafeFor(provider);
    }

    [Fact]
    public void ConfigurationDefault_IsHomeBackups_AndDiagnosticsCreateNothing()
    {
        WriteIni("[paths]\ndata = data\n");
        var report = Load(WithShippedDefaults());

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Equal(Path.Combine(Home, "backups"), report.Settings.BackupsPath);
        Assert.False(Directory.Exists(Path.Combine(Home, "backups")));
        Assert.True(report.Values.Single(v => v is { Section: "paths", Key: "backups" }).Applied);
    }

    [Theory]
    [InlineData("backups = public/backups")]
    [InlineData("backups = conf")]
    [InlineData("backups = conf/old")]
    [InlineData("backups = data")]
    [InlineData("backups = .")]
    public void ConfigurationRejectsUnsafeBackupDirectories(string line)
    {
        WriteIni("[paths]\ndata = data\n" + line + "\n");

        var report = Load(WithShippedDefaults());

        Assert.False(report.IsValid, line);
        Assert.Contains(report.Errors, e => e.StartsWith("[paths]"));
    }

    [Fact]
    public void ConfigurationAcceptsAnAbsoluteBackupDirectoryOutsideHome()
    {
        var elsewhere = Path.Combine(Path.GetTempPath(), "pc-elsewhere-" + Guid.NewGuid().ToString("N"));
        WriteIni($"[paths]\ndata = data\nbackups = {elsewhere}\n");

        var report = Load(WithShippedDefaults());

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Equal(elsewhere, report.Settings.BackupsPath);
        Assert.False(Directory.Exists(elsewhere));
    }

    [Fact]
    public void CommandLine_ParsesBackupCommands_AndRejectsConflicts()
    {
        var o = StartupOptions.Parse(new[] { "--backup-db", "--database", "portal", "--home", "/h" });
        Assert.True(o.BackupDb);
        Assert.Equal("portal", o.Database);
        Assert.Empty(o.Errors);
        Assert.Empty(o.HostArgs);

        var r = StartupOptions.Parse(new[] { "--restore-db", "portal-1.db", "--yes" });
        Assert.Equal("portal-1.db", r.RestoreDb);
        Assert.True(r.Yes);

        Assert.NotEmpty(StartupOptions.Parse(new[] { "--backup-db", "--list-backups" }).Errors);
        Assert.NotEmpty(StartupOptions.Parse(new[] { "--restore-db" }).Errors);
        Assert.NotEmpty(StartupOptions.Parse(new[] { "--backup-db", "--init-db" }).Errors);
    }
}
