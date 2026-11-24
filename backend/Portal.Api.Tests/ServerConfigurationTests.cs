namespace Portal.Api.Tests;

public class ServerConfigurationTests : ServerConfigTestBase
{
    private const string Valid = "[server]\nprotocol = http\nhttp_addr = 127.0.0.1\ndomain = localhost\nport = 5247\n" +
                                 "[paths]\ndata = data\nlogs = logs\npublic = public\nai = ai\n" +
                                 "[log]\nlevel = info\nconsole = true\nfile = true\nmax_file_size_mb = 10\nretain_days = 14\n";

    [Fact]
    public void ValidFile_IsAccepted_AndPathsResolveAgainstHome()
    {
        WriteIni(Valid);
        var report = Load();

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Equal(5247, report.Settings.Port);
        Assert.Equal(Path.Combine(Home, "data"), report.Settings.DataPath);
    }

    [Theory]
    [InlineData("port = 0")]
    [InlineData("port = 65536")]
    [InlineData("port = abc")]
    [InlineData("port = 5247 ; comment")]
    [InlineData("protocol = ftp")]
    [InlineData("http_addr = 999.1.1.1")]
    [InlineData("http_addr = 1")]
    [InlineData("domain = https://host/path")]
    [InlineData("root_url = ftp://host/")]
    [InlineData("root_url = https://host/sub/")]
    public void InvalidServerValue_IsRejected_NamingTheSetting(string line)
    {
        WriteIni("[server]\n" + line + "\n");
        var report = Load();

        Assert.False(report.IsValid);
        var key = line.Split('=')[0].Trim();
        Assert.Contains(report.Errors, e => e.StartsWith($"[server] {key}:"));
    }

    [Theory]
    [InlineData("level = loud")]
    [InlineData("console = maybe")]
    [InlineData("retain_days = 0")]
    [InlineData("max_file_size_mb = -5")]
    public void InvalidLogValue_IsRejected(string line)
    {
        WriteIni("[log]\n" + line + "\n");
        Assert.Contains(Load().Errors, e => e.StartsWith("[log] " + line.Split('=')[0].Trim()));
    }

    [Fact]
    public void UnknownKeyAndSection_AreRejected_WithHint()
    {
        WriteIni("[server]\nprt = 1\n[serv]\nport = 1\n");
        var errors = Load().Errors;

        Assert.Contains(errors, e => e.Contains("[server] prt") && e.Contains("did you mean \"port\""));
        Assert.Contains(errors, e => e.Contains("unknown section [serv]"));
    }

    [Fact]
    public void MalformedAndDuplicateFiles_AreReported_WithoutQuotingContent()
    {
        WriteIni("[server]\nsecret-looking-line-without-equals\n");
        var malformed = Load().Errors.Single();
        Assert.Contains("line 2", malformed);
        Assert.DoesNotContain("secret-looking", malformed);

        WriteIni("[server]\nport = 1\nport = 2\n");
        Assert.Contains(Load().Errors, e => e.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PublicDirectoryContainingPrivateFolders_IsRejected()
    {
        WriteIni("[paths]\npublic = .\n");
        Assert.Contains(Load().Errors, e => e.Contains("private conf directory"));
    }

    [Fact]
    public void RootUrlWithCredentials_IsRejected_AndNeverEchoed()
    {
        WriteIni("[server]\nroot_url = https://user:hunter2@host/\n");
        var report = Load();

        Assert.Contains(report.Errors, e => e.StartsWith("[server] root_url"));
        Assert.DoesNotContain(report.Errors, e => e.Contains("hunter2"));
        Assert.DoesNotContain(report.Values, v => v.Value.Contains("hunter2"));
    }

    [Fact]
    public void MissingFile_IsError_InProduction_ButAllowedInDevelopmentOrWhenOverridden()
    {
        Assert.False(Load("Production").IsValid);
        Assert.True(Load("Development").IsValid);
        Assert.True(Load("Production", "--home", Home, "--allow-missing-config").IsValid);
    }

    [Fact]
    public void ExplicitHomeThatDoesNotExist_IsAlwaysAnError()
    {
        var report = Load("Development", "--home", Path.Combine(Home, "nope"));
        Assert.False(report.IsValid);
    }

    [Fact]
    public void ValidationDoesNotCreateAnyFilesOrDirectories()
    {
        WriteIni(Valid);
        Load();

        Assert.Equal(new[] { "conf" }, Directory.GetDirectories(Home).Select(Path.GetFileName).ToArray());
        Assert.Empty(Directory.GetFiles(Home));
    }
}
