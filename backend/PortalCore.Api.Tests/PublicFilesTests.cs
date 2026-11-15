using System.Net;

namespace PortalCore.Api.Tests;

/// <summary>Stage 5: /config, /navigation and /content from [paths] public, and what must never be reachable.</summary>
public class PublicFilesTests : StaticHostingTestBase
{
    [Fact]
    public async Task PortalIni_IsServedFromTheConfiguredPublicDirectory_AsText()
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var response = await client.GetAsync("/config/portal.ini");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("[portal]\ntitle = Test\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NavigationJson_AndContent_AreServed()
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var json = await client.GetAsync("/navigation/menu.json");
        var html = await client.GetAsync("/content/reports.html");

        Assert.Equal(HttpStatusCode.OK, json.StatusCode);
        Assert.Equal("application/json", json.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        Assert.Equal("text/html", html.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task OnlyTheAllowlistedFolders_AreServed_NotTheRestOfPublic()
    {
        var client = await StartAsync(WebRoot, PublicDir);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/secret/notes.txt")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/public/config/portal.ini")).StatusCode);
    }

    [Theory]
    [InlineData("/conf/server.ini")]
    [InlineData("/data/portalcore.db")]
    [InlineData("/logs/portalcore-1.log")]
    [InlineData("/backups/portal-1.db")]
    [InlineData("/certs/site.pfx")]
    [InlineData("/ai/knowledge/k.csv")]
    [InlineData("/appsettings.json")]
    [InlineData("/bin/appsettings.json")]
    public async Task PrivateFiles_AreNeverServed(string path)
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var response = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("TOP-SECRET", body);
        Assert.DoesNotContain("SQLITE-DATA", body);
    }

    [Theory]
    [InlineData("/config/../conf/server.ini")]
    [InlineData("/config/%2e%2e/conf/server.ini")]
    [InlineData("/config/..%2fconf%2fserver.ini")]
    [InlineData("/config/..%5c..%5cconf%5cserver.ini")]
    [InlineData("/navigation/../../conf/server.ini")]
    [InlineData("/content/%2e%2e/%2e%2e/data/portalcore.db")]
    public async Task PathTraversal_CannotReachPrivateFiles(string path)
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("TOP-SECRET", body);
        Assert.DoesNotContain("SQLITE-DATA", body);
        // Either refused, or answered by the SPA fallback (HTML of the app): never a private file.
        Assert.True(response.StatusCode != HttpStatusCode.OK || body.Contains("REACT-APP"), $"{path} returned {response.StatusCode}");
    }

    [Fact]
    public async Task DirectoryBrowsing_IsOff_AndDotfilesAreNotServed()
    {
        var client = await StartAsync(WebRoot, PublicDir);

        foreach (var path in new[] { "/config", "/config/", "/navigation/", "/assets/", "/content/" })
        {
            var response = await client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("portal.ini", body);
            Assert.DoesNotContain("menu.json", body);
            Assert.DoesNotContain("index-abc.js", body);
        }

        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/config/.hidden")).StatusCode);
    }

    [Fact]
    public async Task PublicFiles_AreNotCopiedIntoTheWebRoot()
    {
        var client = await StartAsync(WebRoot, PublicDir);
        await client.GetAsync("/config/portal.ini");

        Assert.False(Directory.Exists(Path.Combine(WebRoot, "config")));
        Assert.False(File.Exists(Path.Combine(WebRoot, "config", "portal.ini")));
    }

    [Fact]
    public async Task ChangedPublicFiles_AreServedWithoutARebuild()
    {
        var client = await StartAsync(WebRoot, PublicDir);
        Assert.Contains("Test", await client.GetStringAsync("/config/portal.ini"));

        File.WriteAllText(Path.Combine(PublicDir, "config", "portal.ini"), "[portal]\ntitle = Changed\n");

        Assert.Contains("Changed", await client.GetStringAsync("/config/portal.ini"));
    }

    [Fact]
    public async Task PublicDirectoryAbsolutePath_IsUsed_RegardlessOfTheWorkingDirectory()
    {
        var before = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.GetTempPath());
            var client = await StartAsync(WebRoot, PublicDir);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/config/portal.ini")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(before);
        }
    }

    [Fact]
    public async Task MissingPublicFolders_AreSkipped_AndUnknownPublicPathIsAccepted()
    {
        Directory.Delete(Path.Combine(PublicDir, "content"), recursive: true);
        var client = await StartAsync(WebRoot, PublicDir);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/config/portal.ini")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/content/reports.html")).StatusCode); // a file name is never answered by the app

        var none = await StartAsync(WebRoot, null);
        Assert.Equal(HttpStatusCode.OK, (await none.GetAsync("/")).StatusCode);
    }
}
