using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Portal.Api.Configuration.Server;

namespace Portal.Api.Tests;

/// <summary>
/// Builds a real in-process Kestrel host through <see cref="StaticHosting"/>, with a temporary installation:
/// bin/wwwroot (frontend), public/{config,navigation,content}, and private directories next to them.
/// </summary>
public abstract class StaticHostingTestBase : IDisposable
{
    protected string Root { get; } = Path.Combine(Path.GetTempPath(), "portal-static-" + Guid.NewGuid().ToString("N"));
    protected string WebRoot => Path.Combine(Root, "bin", "wwwroot");
    protected string PublicDir => Path.Combine(Root, "public");

    private readonly List<WebApplication> _apps = new();
    private readonly List<HttpClient> _clients = new();

    protected StaticHostingTestBase()
    {
        Write(Path.Combine(WebRoot, "index.html"), "<html><body>REACT-APP</body></html>");
        Write(Path.Combine(WebRoot, "assets", "index-abc.js"), "console.log('app');");
        Write(Path.Combine(WebRoot, "assets", "index-abc.css"), "body{}");
        Write(Path.Combine(WebRoot, "favicon.svg"), "<svg/>");

        Write(Path.Combine(PublicDir, "config", "portal.ini"), "[portal]\ntitle = Test\n");
        Write(Path.Combine(PublicDir, "navigation", "menu.json"), "{\"nodes\":[]}");
        Write(Path.Combine(PublicDir, "content", "reports.html"), "<p>report</p>");
        Write(Path.Combine(PublicDir, "secret", "notes.txt"), "not in the allowlist");

        // Private directories: they sit next to public, and one is even placed inside it by mistake.
        Write(Path.Combine(Root, "conf", "server.ini"), "[tls]\ncert_password = TOP-SECRET\n");
        Write(Path.Combine(Root, "data", "usage.db"), "SQLITE-DATA");
        Write(Path.Combine(Root, "logs", "application-1.log"), "log");
        Write(Path.Combine(Root, "backups", "portal-1.db"), "backup");
        Write(Path.Combine(Root, "certs", "site.pfx"), "pfx");
        Write(Path.Combine(Root, "ai", "knowledge", "k.csv"), "a,b");
        Write(Path.Combine(Root, "bin", "appsettings.json"), "{\"ConnectionStrings\":{}}");
        Write(Path.Combine(PublicDir, "config", ".hidden"), "dotfile");
    }

    protected static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>Starts the host on a dynamic loopback port and returns a client for it.</summary>
    protected async Task<HttpClient> StartAsync(string? webRoot, string? publicPath, bool mapApi = true)
    {
        var options = new WebApplicationOptions { ContentRootPath = Path.Combine(Root, "bin"), WebRootPath = webRoot };
        var builder = WebApplication.CreateBuilder(options);
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(System.Net.IPAddress.Loopback, 0));

        var app = builder.Build();
        StaticHosting.Use(app, publicPath);

        if (mapApi)
        {
            app.MapPost("/api/usage", () => Results.Ok(new { success = true }));
            app.MapGet("/api/knowledge/search", () => Results.Ok(new[] { "hit" }));
        }

        StaticHosting.MapFallback(app);
        await app.StartAsync();
        _apps.Add(app);

        var address = app.Urls.First();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        _clients.Add(client);
        return client;
    }

    public void Dispose()
    {
        foreach (var client in _clients) client.Dispose();
        foreach (var app in _apps) app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}
