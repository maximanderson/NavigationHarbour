using Portal.Api.Configuration;
using Portal.Api.Configuration.Server;
using Serilog.Events;

namespace Portal.Api.Tests;

/// <summary>Stage 2: [log], [paths] logs and [paths] ai are applied to PortalLogging and AiSettings.</summary>
public class ApplyMappingTests : ApplyTestBase
{
    [Fact]
    public void LogSettings_AreMappedToPortalLoggingOptions()
    {
        WriteIni("[log]\nlevel = debug\nconsole = false\nfile = true\nmax_file_size_mb = 3\nretain_days = 5\n");
        var config = WithShippedDefaults();

        var report = Load(config);
        var options = PortalLoggingOptions.Parse(config);

        Assert.True(report.IsValid);
        Assert.Equal(LogEventLevel.Debug, options.Level);
        Assert.False(options.Console);
        Assert.True(options.FileEnabled);
        Assert.Equal(3, options.MaxFileSizeMb);
        Assert.Equal(5, options.RetainDays);
        Assert.Empty(options.Issues);
    }

    [Fact]
    public void LogsPath_RelativeIsResolvedAgainstHome_NotContentRoot()
    {
        WriteIni("[paths]\nlogs = var/log\n");
        var config = WithShippedDefaults();

        Load(config);

        Assert.Equal(Path.Combine(Home, "var", "log"), config["PortalLogging:File:Directory"]);
        Assert.Equal(Path.Combine(Home, "var", "log"), PortalLoggingOptions.Parse(config).Directory);
    }

    [Fact]
    public void AbsolutePaths_AreUsedUnchanged()
    {
        var logs = Path.Combine(Path.GetTempPath(), "pc-abs-logs");
        var ai = Path.Combine(Path.GetTempPath(), "pc-abs-ai");
        WriteIni($"[paths]\nlogs = {logs}\nai = {ai}\n");
        var config = WithShippedDefaults();

        Load(config);

        Assert.Equal(logs, config["PortalLogging:File:Directory"]);
        Assert.Equal(Path.Combine(ai, "knowledge"), config["AiSettings:KnowledgeFolder"]);
        Assert.Equal(Path.Combine(ai, "prompts"), config["AiSettings:PromptFolder"]);
    }

    [Fact]
    public void AiPath_MapsKnowledgeAndPromptFolders_AgainstHome()
    {
        WriteIni("[paths]\nai = ai\n");
        var config = WithShippedDefaults();

        Load(config);

        Assert.Equal(Path.Combine(Home, "ai", "knowledge"), config["AiSettings:KnowledgeFolder"]);
        Assert.Equal(Path.Combine(Home, "ai", "prompts"), config["AiSettings:PromptFolder"]);
    }

    [Fact]
    public void InvalidConfiguration_IsNotApplied()
    {
        WriteIni("[log]\nlevel = loud\nretain_days = 3\n");
        var config = WithShippedDefaults();

        var report = Load(config);

        Assert.False(report.IsValid);
        Assert.Equal("14", config["PortalLogging:File:RetainDays"]);
    }

    [Fact]
    public void PortalPaths_ResolveAgainstHome_AndLeaveAbsoluteAndEmptyAlone()
    {
        var absolute = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "x"));

        Assert.Equal(Path.Combine(Home, "a", "b"), PortalPaths.ResolveAgainstHome(Home, "a/b"));
        Assert.Equal(absolute, PortalPaths.ResolveAgainstHome(Home, absolute));
        Assert.Equal("rel", PortalPaths.ResolveAgainstHome(null, "rel"));
        Assert.Equal("", PortalPaths.ResolveAgainstHome(Home, ""));
    }
}
