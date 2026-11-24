using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Portal.Api.Configuration.Server;

namespace Portal.Api.Tests;

/// <summary>
/// Client IP resolution through the standard Forwarded Headers Middleware, configured exactly as Navigation Harbour
/// configures it. The middleware is run against a simulated connection so that untrusted and IPv6 peers can be tested.
/// </summary>
public class ForwardedHeadersTests
{
    private static ServerSettings Settings(string? proxies = null, string? networks = null)
    {
        var settings = new ServerSettings();
        if (proxies is not null)
            settings.TrustedProxies.AddRange(proxies.Split(',').Select(p => IPAddress.Parse(p.Trim())));
        if (networks is not null)
            settings.TrustedNetworks.AddRange(networks.Split(',').Select(n => System.Net.IPNetwork.Parse(n.Trim())));
        return settings;
    }

    /// <summary>Runs one request through the middleware and returns the context as the endpoint would see it.</summary>
    private static async Task<HttpContext> SendAsync(ServerSettings settings, string peer, params string[] forwardedFor)
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(options, settings);

        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        foreach (var value in forwardedFor)
        {
            context.Request.Headers.Append("X-Forwarded-For", value);
        }

        await middleware.Invoke(context);
        return context;
    }

    [Fact]
    public async Task DirectConnection_WithoutForwardedHeader_KeepsThePeerAddress()
    {
        var context = await SendAsync(Settings(), "203.0.113.7");

        Assert.Equal(IPAddress.Parse("203.0.113.7"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task LoopbackProxy_IsTrustedByDefault_WithoutAnyConfiguration()
    {
        var context = await SendAsync(Settings(), "127.0.0.1", "198.51.100.20");

        Assert.Equal(IPAddress.Parse("198.51.100.20"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task Ipv6LoopbackProxy_IsTrustedByDefault()
    {
        var context = await SendAsync(Settings(), "::1", "198.51.100.20");

        Assert.Equal(IPAddress.Parse("198.51.100.20"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task LoopbackPeerWithoutHeader_StaysLoopback()
    {
        var context = await SendAsync(Settings(), "127.0.0.1");

        Assert.Equal(IPAddress.Loopback, context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task UntrustedPeer_CannotSpoofTheClientAddress()
    {
        var context = await SendAsync(Settings(), "203.0.113.7", "1.2.3.4");

        Assert.Equal(IPAddress.Parse("203.0.113.7"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ConfiguredProxyAddress_IsTrusted_AndOthersStayUntrusted()
    {
        var settings = Settings(proxies: "10.0.0.5");

        var trusted = await SendAsync(settings, "10.0.0.5", "198.51.100.20");
        var neighbour = await SendAsync(settings, "10.0.0.6", "198.51.100.20");

        Assert.Equal(IPAddress.Parse("198.51.100.20"), trusted.Connection.RemoteIpAddress);
        Assert.Equal(IPAddress.Parse("10.0.0.6"), neighbour.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ConfiguredNetwork_IsTrusted_AndOutsideItIsNot()
    {
        var settings = Settings(networks: "10.1.0.0/16");

        var inside = await SendAsync(settings, "10.1.200.3", "198.51.100.20");
        var outside = await SendAsync(settings, "10.2.0.3", "198.51.100.20");

        Assert.Equal(IPAddress.Parse("198.51.100.20"), inside.Connection.RemoteIpAddress);
        Assert.Equal(IPAddress.Parse("10.2.0.3"), outside.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task Ipv6_ProxyNetworkAndClient_AreSupported()
    {
        var settings = Settings(proxies: "fd00::5", networks: "fd00:abcd::/64");

        var byAddress = await SendAsync(settings, "fd00::5", "2001:db8::42");
        var byNetwork = await SendAsync(settings, "fd00:abcd::17", "2001:db8::43");
        var untrusted = await SendAsync(settings, "fd00:ffff::1", "2001:db8::44");

        Assert.Equal(IPAddress.Parse("2001:db8::42"), byAddress.Connection.RemoteIpAddress);
        Assert.Equal(IPAddress.Parse("2001:db8::43"), byNetwork.Connection.RemoteIpAddress);
        Assert.Equal(IPAddress.Parse("fd00:ffff::1"), untrusted.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task MixedAddressFamilies_AreSupported()
    {
        var v6Client = await SendAsync(Settings(), "127.0.0.1", "2001:db8::1");
        var v4Client = await SendAsync(Settings(proxies: "fd00::5"), "fd00::5", "198.51.100.20");

        Assert.Equal(IPAddress.Parse("2001:db8::1"), v6Client.Connection.RemoteIpAddress);
        Assert.Equal(IPAddress.Parse("198.51.100.20"), v4Client.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ConfiguredProxies_AreAddedToTheLoopbackDefault_NotReplacingIt()
    {
        var context = await SendAsync(Settings(proxies: "10.0.0.5"), "127.0.0.1", "198.51.100.20");

        Assert.Equal(IPAddress.Parse("198.51.100.20"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task SingleHop_OnlyTheLastEntryIsUsed_SoAClientSuppliedValueIsIgnored()
    {
        // The client sent "X-Forwarded-For: 1.2.3.4"; the trusted proxy appended the address it really saw.
        var context = await SendAsync(Settings(), "127.0.0.1", "1.2.3.4, 198.51.100.20");

        Assert.Equal(IPAddress.Parse("198.51.100.20"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task SingleHop_ChainOfTrustedProxies_IsNotUnwound()
    {
        // Two trusted hops: with ForwardLimit = 1 the address of the second proxy is reported, not the client.
        var context = await SendAsync(Settings(proxies: "10.0.0.5"), "127.0.0.1", "198.51.100.20, 10.0.0.5");

        Assert.Equal(IPAddress.Parse("10.0.0.5"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public void Configuration_ProcessesOnlyXForwardedFor_WithOneHop_AndKeepsLoopbackTrust()
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(options, Settings(proxies: "10.0.0.5"));

        Assert.Equal(ForwardedHeaders.XForwardedFor, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        // Framework default trust: ::1 and 127.0.0.0/8.
        Assert.Contains(IPAddress.IPv6Loopback, options.KnownProxies);
        Assert.Contains(options.KnownIPNetworks, n => n.Contains(IPAddress.Loopback));
        Assert.Contains(IPAddress.Parse("10.0.0.5"), options.KnownProxies);
    }

    [Fact]
    public void Configuration_WithNothingConfigured_LeavesFrameworkTrustDefaultsUntouched()
    {
        var defaults = new ForwardedHeadersOptions();
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(options, new ServerSettings());

        Assert.Equal(defaults.KnownProxies.Count, options.KnownProxies.Count);
        Assert.Equal(defaults.KnownIPNetworks.Count, options.KnownIPNetworks.Count);
    }

    // One end-to-end check on a real Kestrel host, reading the address the way the usage endpoint does.
    [Fact]
    public async Task RealHost_ReportsForwardedAddressFromLoopbackProxy_AndPeerAddressOtherwise()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
        builder.Services.Configure<ForwardedHeadersOptions>(o => ForwardedHeadersSetup.Configure(o, new ServerSettings()));

        await using var app = builder.Build();
        app.UseForwardedHeaders();
        app.MapGet("/ip", (HttpContext http) => http.Connection.RemoteIpAddress?.ToString());
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        Assert.Equal("127.0.0.1", await client.GetStringAsync("/ip"));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/ip");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");
        using var response = await client.SendAsync(request);
        Assert.Equal("203.0.113.9", await response.Content.ReadAsStringAsync());

        await app.StopAsync();
    }
}
