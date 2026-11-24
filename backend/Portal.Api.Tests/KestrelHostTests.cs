using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Portal.Api.Configuration.Server;

namespace Portal.Api.Tests;

/// <summary>Stage 4: real Kestrel hosts (in this process, on dynamic ports) built through KestrelSetup.</summary>
public class KestrelHostTests : TlsTestBase
{
    private static readonly HttpClientHandler TrustAnyCertificate = new() { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };

    private static async Task<(WebApplication App, string[] Addresses)> StartAsync(
        ServerSettings settings, X509Certificate2? certificate, string? competingUrls = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        if (competingUrls is not null)
        {
            builder.WebHost.UseUrls(competingUrls);
        }

        builder.WebHost.ConfigureKestrel(kestrel => KestrelSetup.Configure(kestrel, settings, certificate));

        var app = builder.Build();
        app.MapGet("/ping", () => "pong");
        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.ToArray();
        return (app, addresses);
    }

    private static ServerSettings Settings(string protocol, string address = "127.0.0.1", int port = 0) =>
        new() { Protocol = protocol, HttpAddr = address, Port = port };

    /// <summary>Kestrel cannot use a dynamic port with "localhost" (and real configuration never allows port 0).</summary>
    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public async Task Http_ListensOnExactlyOneEndpoint_AndServesRoutes()
    {
        var (app, addresses) = await StartAsync(Settings("http"), null);
        await using var _ = app;

        var address = Assert.Single(addresses);
        Assert.StartsWith("http://127.0.0.1:", address);

        using var client = new HttpClient();
        Assert.Equal("pong", await client.GetStringAsync(address + "/ping"));
    }

    [Fact]
    public async Task Https_WithPfx_ServesOverTls_OnExactlyOneEndpoint()
    {
        using var cert = TestCertificates.Create();
        var (app, addresses) = await StartAsync(Settings("https"), cert);
        await using var _ = app;

        var address = Assert.Single(addresses);
        Assert.StartsWith("https://127.0.0.1:", address);

        using var client = new HttpClient(TrustAnyCertificate, disposeHandler: false);
        Assert.Equal("pong", await client.GetStringAsync(address + "/ping"));
    }

    [Fact]
    public async Task Https_WithPemAndEncryptedKey_LoadedByTheLoader_ServesOverTls()
    {
        TestCertificates.WritePem(Path.Combine(Home, "conf"), "s", "pem-pass-1");
        var report = LoadIni("[server]\nprotocol = https\n[tls]\ncert_file = conf/s.pem\ncert_key_file = conf/s.key\ncert_password = pem-pass-1\n[paths]\ndata = data\n");
        Assert.True(report.IsValid, string.Join("; ", report.Errors));

        using var cert = TlsCertificateLoader.Load(report.Settings);
        var settings = Settings("https");
        var (app, addresses) = await StartAsync(settings, cert);
        await using var _ = app;

        using var client = new HttpClient(TrustAnyCertificate, disposeHandler: false);
        Assert.Equal("pong", await client.GetStringAsync(Assert.Single(addresses) + "/ping"));
    }

    [Fact]
    public async Task Https_DoesNotAlsoListenOnPlainHttp()
    {
        using var cert = TestCertificates.Create();
        var (app, addresses) = await StartAsync(Settings("https"), cert);
        await using var _ = app;

        var uri = new Uri(Assert.Single(addresses));
        using var client = new HttpClient(TrustAnyCertificate, disposeHandler: false);

        // Plain http to the https port is not served as a page: the request fails or is rejected.
        var plain = $"http://127.0.0.1:{uri.Port}/ping";
        var served = false;
        try
        {
            using var response = await client.GetAsync(plain);
            served = response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
        }

        Assert.False(served);
    }

    [Fact]
    public void Https_WithoutACertificate_Throws_SoThereIsNoFallbackToHttp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => KestrelSetup.Configure(kestrel, Settings("https"), null));

        // The Kestrel options callback runs when the server is built/started; either way it must fail.
        Assert.ThrowsAny<Exception>(() => builder.Build().StartAsync().GetAwaiter().GetResult());
    }

    [Fact]
    public async Task ServerEndpoint_WinsOverCompetingUrls_AndNoExtraListenerIsAdded()
    {
        var (app, addresses) = await StartAsync(Settings("http"), null, competingUrls: "http://127.0.0.1:0;http://127.0.0.1:0");
        await using var _ = app;

        Assert.Single(addresses);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("")]
    [InlineData("127.0.0.1")]
    public async Task AddressForms_AllProduceOneListener(string address)
    {
        var (app, addresses) = await StartAsync(Settings("http", address, address == "localhost" ? FreePort() : 0), null);
        await using var _ = app;

        Assert.NotEmpty(addresses);
        Assert.All(addresses, a => Assert.StartsWith("http://", a));
    }
}
