using System.Net;

namespace PortalCore.Api.Tests;

/// <summary>Stage 5: frontend hosting, public allowlist, API 404 handling and SPA fallback.</summary>
public class StaticHostingTests : StaticHostingTestBase
{
    [Fact]
    public async Task Root_ServesTheReactApplication()
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("REACT-APP", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task JavaScriptCssAndOtherAssets_AreServedWithTheRightTypes()
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var js = await client.GetAsync("/assets/index-abc.js");
        var css = await client.GetAsync("/assets/index-abc.css");
        var svg = await client.GetAsync("/favicon.svg");

        Assert.Equal(HttpStatusCode.OK, js.StatusCode);
        Assert.Contains("javascript", js.Content.Headers.ContentType!.MediaType);
        Assert.Equal("text/css", css.Content.Headers.ContentType!.MediaType);
        Assert.Equal("image/svg+xml", svg.Content.Headers.ContentType!.MediaType);
    }

    [Theory]
    [InlineData("/screens/scada")]
    [InlineData("/some/deep/client/route")]
    [InlineData("/index.html")]
    public async Task ClientSideRoutes_FallBackToTheReactApplication(string path)
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("REACT-APP", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/assets/missing.js")]
    [InlineData("/assets/missing.css")]
    [InlineData("/missing.png")]
    public async Task MissingStaticAssets_Return404_NotTheReactApplication(string path)
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("REACT-APP", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/api/usage/extra/segments")]
    [InlineData("/api")]
    public async Task UnknownApiPaths_ReturnAJson404_NotHtml(string path)
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain("REACT-APP", body);
        Assert.Contains("Not found", body);
    }

    [Fact]
    public async Task ExistingApiRoutes_KeepWorking()
    {
        var client = await StartAsync(WebRoot, PublicDir);

        var post = await client.PostAsync("/api/usage", new StringContent("{}"));
        var get = await client.GetAsync("/api/knowledge/search");

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        Assert.Contains("success", await post.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task WithoutACompiledFrontend_NothingIsServedAtRoot_LikeDevelopment()
    {
        var client = await StartAsync(Path.Combine(Root, "no-such-wwwroot"), PublicDir);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/screens/scada")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/config/portal.ini")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/nope")).StatusCode);
    }
}
