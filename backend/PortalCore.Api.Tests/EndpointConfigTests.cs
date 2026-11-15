using Microsoft.Extensions.Configuration;
using PortalCore.Api.Configuration.Server;

namespace PortalCore.Api.Tests;

/// <summary>Stage 4: validation of the [server] endpoint. Nothing is started or loaded.</summary>
public class EndpointConfigTests : TlsTestBase
{
    [Fact]
    public void Http_EndpointFromServerIni_IsExplicitAndApplied()
    {
        var report = LoadIni("[server]\nprotocol = http\nhttp_addr = 127.0.0.1\nport = 5300\n[paths]\ndata = data\n");

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.True(report.Settings.ExplicitEndpoint);
        Assert.Equal(5300, report.Settings.Port);
        Assert.Equal("http://127.0.0.1:5300", KestrelSetup.Describe(report.Settings));
        Assert.True(report.Values.Single(v => v is { Section: "server", Key: "port" }).Applied);
    }

    [Fact]
    public void NoEndpointSettings_KeepsTheDevelopmentBehaviour_AndNothingIsApplied()
    {
        var report = LoadIni("[paths]\ndata = data\n");

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.False(report.Settings.ExplicitEndpoint);
        Assert.All(report.Values.Where(v => v.Section is "server" or "tls"), v => Assert.False(v.Applied));
    }

    [Fact]
    public void AnyConfigurationSource_CanMakeTheEndpointExplicit()
    {
        WriteIni("[paths]\ndata = data\n");
        var config = WithShippedDefaults();
        config.AddInMemoryCollection(new Dictionary<string, string?> { ["server:port"] = "5311" });

        var report = Load(config);

        Assert.True(report.Settings.ExplicitEndpoint);
        Assert.Equal(5311, report.Settings.Port);
    }

    [Theory]
    [InlineData("http_addr = 999.1.1.1")]
    [InlineData("http_addr = example.com")]
    [InlineData("http_addr = 1")]
    [InlineData("port = 0")]
    [InlineData("port = 70000")]
    [InlineData("port = abc")]
    [InlineData("port = 80 ; main")]
    [InlineData("protocol = ftp")]
    public void InvalidEndpointSettings_AreErrors(string line)
    {
        var report = LoadIni($"[server]\n{line}\n[paths]\ndata = data\n");

        Assert.False(report.IsValid, line);
        Assert.Contains(report.Errors, e => e.StartsWith("[server]"));
    }

    [Theory]
    [InlineData("cert_password", true)]
    [InlineData("cert_key_file", false)]
    [InlineData("cert_file", false)]
    [InlineData("api_key", true)]
    [InlineData("key", true)]
    [InlineData("token", true)]
    [InlineData("secret_value", true)]
    [InlineData("port", false)]
    public void SecretDetection_TreatsPasswordsAsSecret_AndKeyFilesAsPaths(string key, bool secret)
    {
        Assert.Equal(secret, ServerSchema.IsSecret(key));
    }
}
