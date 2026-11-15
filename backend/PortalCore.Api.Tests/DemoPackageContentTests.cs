using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace PortalCore.Api.Tests;

/// <summary>Stage 7: demo/ configuration and files are demonstration-only and free of real operational information.</summary>
public class DemoPackageContentTests
{
    private static string Demo(params string[] parts) => DemoNavigationTests.Demo(parts);

    [Fact]
    public void DemoConfig_ReferencesOnlyExistingDemoFiles()
    {
        var ini = new ConfigurationBuilder().AddIniFile(Demo("config", "portal.ini")).Build();
        var sources = ini.AsEnumerable()
            .Where(p => p.Value is not null && (p.Key.EndsWith(":source") || p.Key.EndsWith(":branchesSource")))
            .Select(p => p.Value!).ToList();

        Assert.True(sources.Count >= 4);
        foreach (var source in sources)
        {
            var parts = source.TrimStart('/').Split('/');
            Assert.Contains(parts[0], new[] { "navigation", "content" });
            Assert.True(File.Exists(Demo(parts)), $"{source} does not exist in demo/");
        }
    }

    [Fact]
    public void DemoConfig_HasSameOriginApiSettings_AndTheAiAssistantIsOff()
    {
        var ini = new ConfigurationBuilder().AddIniFile(Demo("config", "portal.ini")).Build();

        foreach (var section in new[] { "usage_monitor", "ai" })
        {
            Assert.Null(ini[$"{section}:host"]);
            Assert.Null(ini[$"{section}:port"]);
        }

        Assert.Equal("true", ini["usage_monitor:enabled"]);
        Assert.Equal("false", ini["ai:enabled"]);
        Assert.Equal("/api/usage", ini["usage_monitor:endpoint"]);
    }

    [Fact]
    public void DemoFiles_MatchNoReleaseScanTextRule()
    {
        // The same rules the release build enforces (scripts/release-scan-rules.txt), applied to demo/.
        var rules = File.ReadAllLines(Path.Combine(DemoNavigationTests.Root, "scripts", "release-scan-rules.txt"))
            .Where(l => l.StartsWith("text: ")).Select(l => new Regex(l[6..], RegexOptions.IgnoreCase)).ToList();
        Assert.NotEmpty(rules);

        foreach (var file in Directory.EnumerateFiles(Demo(), "*", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var rule in rules)
            {
                Assert.False(rule.IsMatch(text), $"{Path.GetRelativePath(DemoNavigationTests.Root, file)} matches a release scan rule.");
            }
        }
    }

    [Fact]
    public void DemoDirectory_HasExactlyTheAllowlistedFiles()
    {
        var files = Directory.EnumerateFiles(Demo(), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Demo(), f).Replace('\\', '/')).Order().ToList();

        Assert.Equal(new[]
        {
            "ai/knowledge/csv/cities.csv",
            "ai/prompts/csv/contextPrompt.txt",
            "config/portal.ini",
            "content/reports.html",
            "navigation/demo-branches.json",
            "navigation/demo-navigation.json",
        }, files);
    }
}
