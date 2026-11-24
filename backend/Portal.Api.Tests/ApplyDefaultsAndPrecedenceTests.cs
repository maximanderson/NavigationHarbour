using Microsoft.Extensions.Configuration;

namespace Portal.Api.Tests;

/// <summary>Stage 2: development defaults, precedence and "no side effects".</summary>
public class ApplyDefaultsAndPrecedenceTests : ApplyTestBase
{
    [Fact]
    public void NothingConfigured_KeepsDevelopmentDefaults()
    {
        var config = WithShippedDefaults();

        var report = Load(config, "Development");

        Assert.True(report.IsValid);
        Assert.Equal("logs", config["PortalLogging:File:Directory"]);
        Assert.Equal("Data/knowledge", config["AiSettings:KnowledgeFolder"]);
        Assert.Equal("Data/prompts", config["AiSettings:PromptFolder"]);
        Assert.Equal("info", config["PortalLogging:Level"]);
    }

    [Fact]
    public void UnconfiguredAi_IsReportedAsNotSet_AndEffectiveFoldersComeFromContentRoot()
    {
        WriteIni("[log]\nlevel = info\n");
        var report = Load(WithShippedDefaults());

        var ai = report.Values.Single(v => v is { Section: "paths", Key: "ai" });
        Assert.Equal("(not set)", ai.Value);
        Assert.Contains(Path.Combine(ContentRoot, "Data", "knowledge"), ai.ResolvedPath);
    }

    [Fact]
    public void OnlyExplicitSettings_AreMapped()
    {
        WriteIni("[log]\nlevel = warning\n");
        var config = WithShippedDefaults();

        Load(config);

        Assert.Equal("warning", config["PortalLogging:Level"]);
        // Not in server.ini, so the shipped appsettings.json values are untouched.
        Assert.Equal("14", config["PortalLogging:File:RetainDays"]);
        Assert.Equal("logs", config["PortalLogging:File:Directory"]);
        Assert.Equal("Data/knowledge", config["AiSettings:KnowledgeFolder"]);
    }

    [Fact]
    public void Precedence_ServerIniOverridesAppSettings_ButCommandLineOverridesServerIni()
    {
        WriteIni("[log]\nlevel = debug\nretain_days = 7\n");

        var config = WithShippedDefaults();
        config.AddCommandLine(new[] { "--PortalLogging:Level=error" });

        var report = Load(config);

        Assert.Equal("error", config["PortalLogging:Level"]);
        Assert.Equal("7", config["PortalLogging:File:RetainDays"]);
        Assert.Contains(report.Warnings, w => w.Contains("PortalLogging:Level") && w.Contains("overridden"));
    }

    [Fact]
    public void Report_MarksAppliedAndValidatedOnlySettings()
    {
        WriteIni("[server]\nport = 5300\n");
        var report = Load(WithShippedDefaults());

        bool Applied(string section, string key) =>
            report.Values.Single(v => v.Section == section && v.Key == key).Applied;

        Assert.True(Applied("log", "level"));
        Assert.True(Applied("paths", "logs"));
        Assert.True(Applied("paths", "ai"));
        Assert.True(Applied("server", "port")); // Stage 4: [server] sets the endpoint, so Kestrel applies it
        Assert.False(Applied("server", "domain")); // still validated only
        Assert.False(Applied("server", "root_url"));
        Assert.True(Applied("paths", "data")); // Stage 3A: SQLite location
        Assert.False(Applied("paths", "public"));
    }

    [Fact]
    public void Applying_DoesNotCreateAnyDirectories()
    {
        WriteIni("[paths]\nlogs = logs\nai = ai\n");
        Load(WithShippedDefaults());

        Assert.False(Directory.Exists(Path.Combine(Home, "logs")));
        Assert.False(Directory.Exists(Path.Combine(Home, "ai")));
        Assert.False(Directory.Exists(ContentRoot));
    }
}
