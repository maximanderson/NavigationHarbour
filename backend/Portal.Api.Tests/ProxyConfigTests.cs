using System.Net;
using Microsoft.Extensions.Configuration;

namespace Portal.Api.Tests;

/// <summary>[proxy] in server.ini: defaults, backward compatibility, valid and invalid values.</summary>
public class ProxyConfigTests : ServerConfigTestBase
{
    [Fact]
    public void ExistingConfigWithoutProxySection_StaysValid_AndKeepsLoopbackOnlyTrust()
    {
        WriteIni("[server]\nport = 5247\n[log]\nlevel = info\n");
        var report = Load();

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Empty(report.Settings.TrustedProxies);
        Assert.Empty(report.Settings.TrustedNetworks);
    }

    [Fact]
    public void NoConfigFileAtAll_UsesDefaults()
    {
        var report = Load();

        Assert.Empty(report.Settings.TrustedProxies);
        Assert.Empty(report.Settings.TrustedNetworks);
    }

    [Fact]
    public void EmptyProxyValues_AreValid_AndMeanDefaultTrust()
    {
        WriteIni("[proxy]\ntrusted_proxies =\ntrusted_networks =\n");
        var report = Load();

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Empty(report.Settings.TrustedProxies);
        Assert.Empty(report.Settings.TrustedNetworks);
    }

    [Fact]
    public void ValidProxiesAndNetworks_AreParsed_ForIpv4AndIpv6()
    {
        WriteIni("[proxy]\ntrusted_proxies = 10.0.0.5, fd00::5 ,192.168.1.10\ntrusted_networks = 10.1.0.0/16, fd00:abcd::/64\n");
        var report = Load();

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Equal(
            new[] { IPAddress.Parse("10.0.0.5"), IPAddress.Parse("fd00::5"), IPAddress.Parse("192.168.1.10") },
            report.Settings.TrustedProxies);
        Assert.Equal(new[] { "10.1.0.0/16", "fd00:abcd::/64" }, report.Settings.TrustedNetworks.Select(n => n.ToString()));
    }

    [Theory]
    [InlineData("trusted_proxies = not-an-ip")]
    [InlineData("trusted_proxies = 10.0.0")]
    [InlineData("trusted_proxies = 1")]
    [InlineData("trusted_proxies = 10.0.0.1/24")]
    [InlineData("trusted_proxies = proxy.example.com")]
    [InlineData("trusted_proxies = 10.0.0.1,,10.0.0.2")]
    [InlineData("trusted_proxies = 10.0.0.1, ")]
    [InlineData("trusted_proxies = 10.0.0.1 ; my proxy")]
    [InlineData("trusted_networks = 10.0.0.0")]
    [InlineData("trusted_networks = 10.0.0.0/33")]
    [InlineData("trusted_networks = 10.0.0.5/24")]
    [InlineData("trusted_networks = fd00::/129")]
    [InlineData("trusted_networks = 10.0.0.0/abc")]
    [InlineData("trusted_networks = /24")]
    [InlineData("trusted_networks = 10.0.0.0/8/8")]
    public void InvalidValues_AreRejected_WithAClearMessage(string line)
    {
        WriteIni("[proxy]\n" + line + "\n");
        var key = line.Split('=')[0].Trim();

        Assert.Contains(Load().Errors, e => e.StartsWith($"[proxy] {key}: "));
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    public void TrustingEveryAddress_IsRefused(string network)
    {
        WriteIni("[proxy]\ntrusted_networks = " + network + "\n");

        Assert.Contains(Load().Errors, e => e.Contains("would trust every address"));
    }

    [Fact]
    public void UnknownProxyKey_IsRejected_WithHint()
    {
        WriteIni("[proxy]\ntrusted_proxes = 10.0.0.5\n");

        Assert.Contains(Load().Errors, e => e.Contains("[proxy] trusted_proxes") && e.Contains("did you mean \"trusted_proxies\""));
    }

    [Fact]
    public void ProxySettings_AreShownByPrintConfig_AsApplied()
    {
        WriteIni("[proxy]\ntrusted_proxies = 10.0.0.5\n");
        var report = Load();

        var view = Assert.Single(report.Values, v => v.Key == "trusted_proxies");
        Assert.Equal("10.0.0.5", view.Value);
        Assert.True(view.Applied);
    }

    [Fact]
    public void ProxyValues_CanComeFromTheEnvironmentStyleSource()
    {
        WriteIni("[log]\nlevel = info\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?> { ["proxy:trusted_networks"] = "10.9.0.0/16" });

        var report = Load(config);

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Single(report.Settings.TrustedNetworks);
    }
}
