using Portal.Api.Configuration.Server;

namespace Portal.Api.Tests;

public class HomeAndArgumentTests : ServerConfigTestBase
{
    [Fact]
    public void Arguments_SeparateOwnSwitchesFromHostArguments()
    {
        var o = StartupOptions.Parse(new[] { "--check-config", "--home", "/x", "--urls=http://a", "--print-config" });

        Assert.True(o.CheckConfig);
        Assert.True(o.PrintConfig);
        Assert.Equal("/x", o.Home);
        Assert.Equal(new[] { "--urls=http://a" }, o.HostArgs);
        Assert.Empty(o.Errors);
    }

    [Theory]
    [InlineData("--home")]
    [InlineData("--home=")]
    public void HomeWithoutValue_IsReported(string arg)
    {
        Assert.NotEmpty(StartupOptions.Parse(new[] { arg }).Errors);
    }

    [Fact]
    public void ExplicitHome_BeatsEnvironmentVariable()
    {
        var other = Path.Combine(Home, "other");
        Directory.CreateDirectory(other);

        var result = PortalHome.Resolve(Home, other, "/nowhere");

        Assert.Equal(Home, result.Path);
        Assert.Contains("--home", result.Source);
    }

    [Fact]
    public void EnvironmentVariable_IsUsed_WhenNoArgument()
    {
        var result = PortalHome.Resolve(null, Home, "/nowhere");
        Assert.Equal(Home, result.Path);
        Assert.Contains(PortalHome.EnvironmentVariable, result.Source);
    }

    [Fact]
    public void InstallLayout_IsDetected_FromBinFolder()
    {
        var bin = Path.Combine(Home, "bin");
        Directory.CreateDirectory(bin);

        var result = PortalHome.Resolve(null, null, bin + Path.DirectorySeparatorChar);

        Assert.Equal(Home, result.Path);
        Assert.Equal("installation layout", result.Source);
    }

    [Fact]
    public void DevelopmentProject_IsFound_ByWalkingUp()
    {
        var output = Path.Combine(Home, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(Home, PortalHome.ProjectFileName), "<Project />");
        Directory.Delete(Path.Combine(Home, "conf"));

        var result = PortalHome.Resolve(null, null, output);

        Assert.Equal(Home, result.Path);
        Assert.Equal("development project directory", result.Source);
    }

    [Fact]
    public void Resolution_DoesNotDependOnWorkingDirectory()
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            var first = PortalHome.Resolve(Home, null, "/nowhere");
            Directory.SetCurrentDirectory(Path.GetTempPath());
            var second = PortalHome.Resolve(Home, null, "/nowhere");

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public void NothingFound_IsReportedAsUnresolved()
    {
        var empty = Path.Combine(Home, "empty");
        Directory.CreateDirectory(empty);

        var result = PortalHome.Resolve(null, null, empty);

        Assert.True(result.IsUnresolved);
        Assert.Null(result.Path);
    }
}
