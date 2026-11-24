using Portal.Api.Configuration.Server;

namespace Portal.Api.Tests;

/// <summary>Stage 3A.1: content root is deterministic and separate from Navigation Harbour home.</summary>
public class ContentRootTests : ServerConfigTestBase
{
    [Fact]
    public void Development_ContentRoot_IsTheProjectDirectory()
    {
        var output = Path.Combine(Home, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(Home, PortalHome.ProjectFileName), "<Project />");

        Assert.Equal(Home, PortalHome.ResolveContentRoot(output + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Installation_ContentRoot_IsTheBinDirectory_NotHome()
    {
        var bin = Path.Combine(Home, "bin");
        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(Path.Combine(Home, "conf"));

        var contentRoot = PortalHome.ResolveContentRoot(bin + Path.DirectorySeparatorChar);
        var home = PortalHome.Resolve(null, null, bin + Path.DirectorySeparatorChar);

        Assert.Equal(bin, contentRoot);
        Assert.Equal(Home, home.Path);
        Assert.NotEqual(home.Path, contentRoot);
    }

    [Fact]
    public void ExplicitHome_DoesNotMoveTheContentRoot()
    {
        var bin = Path.Combine(Home, "bin");
        Directory.CreateDirectory(bin);
        var elsewhere = Path.Combine(Home, "elsewhere");
        Directory.CreateDirectory(elsewhere);

        Assert.Equal(bin, PortalHome.ResolveContentRoot(bin));
        Assert.Equal(elsewhere, PortalHome.Resolve(elsewhere, null, bin).Path);
    }

    [Fact]
    public void ContentRoot_DoesNotDependOnWorkingDirectory()
    {
        var bin = Path.Combine(Home, "bin");
        Directory.CreateDirectory(bin);
        var original = Directory.GetCurrentDirectory();
        try
        {
            var first = PortalHome.ResolveContentRoot(bin);
            Directory.SetCurrentDirectory(Path.GetTempPath());

            Assert.Equal(first, PortalHome.ResolveContentRoot(bin));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public void ReportKeepsContentRootAndHomeSeparate()
    {
        var report = Load();
        var text = new StringWriter();
        ServerConfigCommands.Run(StartupOptions.Parse(new[] { "--check-config" }), report, text);

        Assert.Equal(ContentRoot, report.ContentRoot);
        Assert.NotEqual(report.Home.Path, report.ContentRoot);
        Assert.Contains("Content root:", text.ToString());
        Assert.Contains("Home        :", text.ToString());
    }
}
