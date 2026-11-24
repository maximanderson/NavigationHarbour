using Microsoft.Extensions.Configuration;
using Portal.Api.Configuration.Server;

namespace Portal.Api.Tests;

/// <summary>Process-wide environment variables are changed here, so these tests never run in parallel with others.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class EnvironmentVariableCollection
{
    public const string Name = "Environment variables";
}

/// <summary>
/// PORTAL_ is the current prefix; PORTALCORE_ is the pre-rename prefix and must keep working
/// (existing services and scripts). When both are set, PORTAL_ wins.
/// </summary>
[Collection(EnvironmentVariableCollection.Name)]
public class EnvironmentCompatibilityTests : ServerConfigTestBase
{
    private const string NewPort = "PORTAL_server__port";
    private const string OldPort = "PORTALCORE_server__port";

    private static string? Set(string name, string? value)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        return previous;
    }

    private static void WithEnvironment(string? newPort, string? oldPort, Action body)
    {
        var previousNew = Set(NewPort, newPort);
        var previousOld = Set(OldPort, oldPort);
        try
        {
            body();
        }
        finally
        {
            Set(NewPort, previousNew);
            Set(OldPort, previousOld);
        }
    }

    private string? LoadedPort()
    {
        var config = new ConfigurationManager();
        Load(config);
        return config["server:port"];
    }

    [Fact]
    public void NewPrefix_IsApplied()
    {
        WithEnvironment("5301", null, () => Assert.Equal("5301", LoadedPort()));
    }

    [Fact]
    public void LegacyPrefix_StillWorks()
    {
        WithEnvironment(null, "5302", () => Assert.Equal("5302", LoadedPort()));
    }

    [Fact]
    public void NewPrefix_BeatsLegacyPrefix()
    {
        WithEnvironment("5303", "5304", () => Assert.Equal("5303", LoadedPort()));
    }

    [Fact]
    public void EnvironmentVariables_BeatServerIni_AndCommandLineBeatsBoth()
    {
        WriteIni("[server]\nport = 5399\n");

        WithEnvironment(null, "5305", () => Assert.Equal("5305", LoadedPort()));

        WithEnvironment("5306", "5305", () =>
        {
            var config = new ConfigurationManager();
            config.AddCommandLine(new[] { "--server:port=5307" });
            Load(config);
            Assert.Equal("5307", config["server:port"]);
        });
    }

    [Fact]
    public void NewHomeVariable_IsPreferred_OverLegacy()
    {
        var values = new Dictionary<string, string?>
        {
            [PortalHome.EnvironmentVariable] = "/new",
            [PortalHome.LegacyEnvironmentVariable] = "/old",
        };

        var (value, name) = PortalHome.ReadEnvironment(k => values.GetValueOrDefault(k));

        Assert.Equal("/new", value);
        Assert.Equal("PORTAL_HOME", name);
    }

    [Fact]
    public void LegacyHomeVariable_IsUsed_WhenNewOneIsMissingOrBlank()
    {
        var values = new Dictionary<string, string?>
        {
            [PortalHome.EnvironmentVariable] = "  ",
            [PortalHome.LegacyEnvironmentVariable] = "/old",
        };

        var (value, name) = PortalHome.ReadEnvironment(k => values.GetValueOrDefault(k));

        Assert.Equal("/old", value);
        Assert.Equal("PORTALCORE_HOME", name);
    }

    [Fact]
    public void NoHomeVariable_GivesNoValue()
    {
        var (value, _) = PortalHome.ReadEnvironment(_ => null);
        Assert.Null(value);
    }

    [Fact]
    public void Home_FromLegacyVariable_NamesTheVariableInTheSource()
    {
        var result = PortalHome.Resolve(null, Home, "/nowhere", PortalHome.LegacyEnvironmentVariable);

        Assert.Equal(Home, result.Path);
        Assert.Contains("PORTALCORE_HOME", result.Source);
    }
}
