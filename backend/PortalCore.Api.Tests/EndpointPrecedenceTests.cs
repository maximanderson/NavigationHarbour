using Microsoft.Extensions.Configuration;
using PortalCore.Api.Configuration.Server;

namespace PortalCore.Api.Tests;

/// <summary>Stage 4: [server] endpoint versus ASPNETCORE_URLS, --urls and Kestrel:Endpoints.</summary>
public class EndpointPrecedenceTests : TlsTestBase
{
    private static string Print(ServerConfigReport report, string option)
    {
        var writer = new StringWriter();
        ServerConfigCommands.Run(StartupOptions.Parse(new[] { option }), report, writer);
        return writer.ToString();
    }

    private ServerConfigReport LoadWith(string ini, params (string Key, string Value)[] extra)
    {
        WriteIni(ini);
        var config = WithShippedDefaults();
        config.AddInMemoryCollection(extra.ToDictionary(e => e.Key, e => (string?)e.Value));
        return Load(config);
    }

    [Fact]
    public void PrintConfig_ShowsTheEndpoint_OrThatItIsNotConfigured()
    {
        var configured = LoadWith("[server]\nport = 5333\n[paths]\ndata = data\n");
        Assert.Contains("http://127.0.0.1:5333", Print(configured, "--check-config"));

        var plain = LoadWith("[paths]\ndata = data\n");
        Assert.Contains("not configured by [server]", Print(plain, "--check-config"));
    }

    [Fact]
    public void CompetingUrls_AreReportedAsAWarning_WithoutTheirValues()
    {
        var report = LoadWith("[server]\nport = 5334\n[paths]\ndata = data\n", ("urls", "http://0.0.0.0:9999"));

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Contains(report.Warnings, w => w.Contains("ASPNETCORE_URLS") && w.Contains("takes precedence"));
        Assert.Single(report.Settings.EndpointWarnings);
        Assert.DoesNotContain("9999", Print(report, "--print-config"));
        Assert.DoesNotContain("9999", report.Settings.EndpointWarnings[0]);
    }

    [Fact]
    public void CompetingPortSettings_AreAlsoReported()
    {
        var report = LoadWith("[server]\nport = 5334\n[paths]\ndata = data\n", ("http_ports", "8080"), ("https_ports", "8443"));

        Assert.Contains(report.Warnings, w => w.Contains("ASPNETCORE_HTTP_PORTS") && w.Contains("ASPNETCORE_HTTPS_PORTS"));
    }

    [Fact]
    public void CompetingUrls_AreLeftAlone_WhenServerDoesNotConfigureTheEndpoint()
    {
        var report = LoadWith("[paths]\ndata = data\n", ("urls", "http://0.0.0.0:9999"));

        Assert.False(report.Settings.ExplicitEndpoint);
        Assert.DoesNotContain(report.Warnings, w => w.Contains("ASPNETCORE_URLS"));
        Assert.Empty(report.Settings.EndpointWarnings);
    }

    [Fact]
    public void KestrelEndpointsSection_WithAnExplicitServerEndpoint_IsAnError()
    {
        var report = LoadWith("[server]\nport = 5335\n[paths]\ndata = data\n", ("Kestrel:Endpoints:Http:Url", "http://0.0.0.0:8080"));

        Assert.Contains(report.Errors, e => e.Contains("Kestrel:Endpoints"));
    }

    [Fact]
    public void KestrelEndpointsSection_WithoutAServerEndpoint_IsNotTouched()
    {
        var report = LoadWith("[paths]\ndata = data\n", ("Kestrel:Endpoints:Http:Url", "http://0.0.0.0:8080"));

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
    }

    [Fact]
    public void DefaultsOnly_NothingIsConfiguredByServer_AndTheDocumentedDefaultsAreStillValidated()
    {
        var report = LoadWith("[paths]\ndata = data\n");

        Assert.False(report.Settings.ExplicitEndpoint);
        Assert.Equal("http", report.Settings.Protocol);
        Assert.Equal(5247, report.Settings.Port);
        Assert.Equal("127.0.0.1", report.Settings.HttpAddr);
    }
}
