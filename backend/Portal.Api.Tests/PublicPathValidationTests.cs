namespace Portal.Api.Tests;

/// <summary>Stage 5: [paths] public may never contain private directories, the content root or the whole home.</summary>
public class PublicPathValidationTests : TlsTestBase
{
    [Theory]
    [InlineData("public = .")]
    [InlineData("public = conf")]
    [InlineData("public = data")]
    [InlineData("public = logs")]
    [InlineData("public = backups")]
    [InlineData("public = ai")]
    [InlineData("public = ..")]
    public void PublicPath_ThatContainsOrIsAPrivateDirectoryOrTheHome_IsRejected(string line)
    {
        var report = LoadIni($"[paths]\ndata = data\nai = ai\n{line}\n");

        Assert.False(report.IsValid, line);
        Assert.Contains(report.Errors, e => e.StartsWith("[paths] public"));
    }

    [Fact]
    public void PublicPath_ThatContainsTheContentRoot_IsRejected()
    {
        // The content root is the application directory (bin/ when installed): appsettings.json lives there.
        var report = LoadIni("[paths]\ndata = data\npublic = content-root\n");

        Assert.Contains(report.Errors, e => e.Contains("application directory"));
    }

    [Fact]
    public void PublicPath_WithItsOwnFolder_IsAccepted()
    {
        var report = LoadIni("[paths]\ndata = data\nai = ai\npublic = public\n");

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Equal(Path.Combine(Home, "public"), report.Settings.PublicPath);
    }
}
