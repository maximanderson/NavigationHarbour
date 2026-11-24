using Microsoft.Extensions.Configuration;

namespace Portal.Api.Tests;

/// <summary>
/// Stage 6: deploy/portal.standalone.ini is the development portal.ini without host and port for the API
/// sections (same-origin calls). The two files must stay identical otherwise.
/// </summary>
public class StandaloneTemplateTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Portal.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Portal.sln not found above the test binaries.");
    }

    private static Dictionary<string, string?> Read(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot(), relativePath);
        var ini = new ConfigurationBuilder().AddIniFile(path, optional: false).Build();
        return ini.AsEnumerable().Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static readonly string[] ApiSections = { "usage_monitor", "ai" };
    private static readonly string[] SameOriginOnlyKeys = { "host", "port" };

    [Fact]
    public void StandaloneTemplate_HasNoHostOrPort_ForTheApiSections()
    {
        var standalone = Read("deploy/portal.standalone.ini");

        foreach (var section in ApiSections)
        {
            foreach (var key in SameOriginOnlyKeys)
            {
                Assert.DoesNotContain($"{section}:{key}", standalone.Keys);
            }

            Assert.Contains($"{section}:endpoint", standalone.Keys);
        }
    }

    [Fact]
    public void StandaloneTemplate_DiffersFromTheDevelopmentFile_OnlyByHostAndPort()
    {
        var development = Read("frontend/public/config/portal.ini");
        var standalone = Read("deploy/portal.standalone.ini");

        var expectedMissing = ApiSections.SelectMany(s => SameOriginOnlyKeys.Select(k => $"{s}:{k}")).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            development.Keys.Where(k => !expectedMissing.Contains(k)).Order(),
            standalone.Keys.Order());

        foreach (var (key, value) in standalone)
        {
            Assert.Equal(development[key], value);
        }
    }

    [Fact]
    public void DevelopmentFile_KeepsItsExplicitLocalhostApi_ForVite()
    {
        var development = Read("frontend/public/config/portal.ini");

        Assert.Equal("localhost", development["usage_monitor:host"]);
        Assert.Equal("5247", development["usage_monitor:port"]);
        Assert.Equal("localhost", development["ai:host"]);
    }
}
