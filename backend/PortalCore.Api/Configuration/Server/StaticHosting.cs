using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace PortalCore.Api.Configuration.Server;

/// <summary>
/// Stage 5: standalone hosting of the React frontend and the public portal files by standard ASP.NET Core
/// static file middleware. Nothing here is a custom file server, and nothing is copied.
///
/// - The compiled frontend is served from the web root (bin/wwwroot) when it contains index.html.
/// - Only the folders in <see cref="PublicFolders"/> of [paths] public are served, each at its own URL
///   (/config, /navigation, /content). The rest of public, and every private directory, is not reachable.
/// - Directory browsing is never enabled. Dotfiles are not served.
/// In development neither folder exists next to the backend, so nothing changes there.
/// </summary>
public static class StaticHosting
{
    /// <summary>Sub-folders of [paths] public that are web-accessible, at the URL of the same name.</summary>
    public static readonly string[] PublicFolders = { "config", "navigation", "content" };

    /// <summary>True when the web root holds a compiled frontend.</summary>
    public static bool HasFrontend(string? webRoot) =>
        !string.IsNullOrEmpty(webRoot) && File.Exists(Path.Combine(webRoot, "index.html"));

    /// <summary>
    /// Adds the static file middleware. Returns what is hosted, for the startup log.
    /// Call before routing/endpoints so that real files win; the API and the SPA fallback come after.
    /// </summary>
    public static StaticHostingResult Use(WebApplication app, string? publicPath)
    {
        var frontend = HasFrontend(app.Environment.WebRootPath);
        if (frontend)
        {
            app.UseStaticFiles();
        }

        var folders = new List<string>();
        if (publicPath is not null)
        {
            foreach (var name in PublicFolders)
            {
                var directory = Path.Combine(publicPath, name);
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(directory, Microsoft.Extensions.FileProviders.Physical.ExclusionFilters.Sensitive),
                    RequestPath = "/" + name,
                    ContentTypeProvider = ContentTypes(),
                });
                folders.Add(name);
            }
        }

        return new StaticHostingResult(frontend, folders);
    }

    /// <summary>The standard MIME map plus .ini (served as text, so /config/portal.ini loads).</summary>
    private static FileExtensionContentTypeProvider ContentTypes()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".ini"] = "text/plain; charset=utf-8";
        return provider;
    }

    /// <summary>
    /// SPA fallback: a client-side path (no file extension, not under /api) returns index.html.
    /// A missing file such as /assets/x.js is a real 404, and so is an unknown /api/... path (as JSON).
    /// </summary>
    public static void MapFallback(WebApplication app)
    {
        // Registered before the fallback so that it wins for anything under /api.
        app.Map("/api/{**rest}", (HttpContext http) =>
            Results.Json(new { error = "Not found", path = http.Request.Path.Value }, statusCode: StatusCodes.Status404NotFound));

        if (HasFrontend(app.Environment.WebRootPath))
        {
            app.MapFallbackToFile("index.html");
        }
    }
}

public sealed record StaticHostingResult(bool Frontend, IReadOnlyList<string> PublicFolders);
