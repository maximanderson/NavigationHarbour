namespace PortalCore.Api.Tests;

/// <summary>Stage 4: validation of the [tls] settings and certificate files. Certificates are not loaded.</summary>
public class TlsFileConfigTests : TlsTestBase
{
    private string Conf(string file) => Path.Combine(Home, "conf", file);

    private const string Https = "[server]\nprotocol = https\n";
    private const string Data = "[paths]\ndata = data\n";

    [Fact]
    public void Https_RequiresACertificateFile()
    {
        var report = LoadIni(Https + Data);

        Assert.Contains(report.Errors, e => e.StartsWith("[tls] cert_file: required"));
    }

    [Fact]
    public void Https_MissingCertificateFile_IsAnError_AndRelativePathsResolveAgainstHome()
    {
        var missing = LoadIni(Https + "[tls]\ncert_file = conf/none.pfx\n" + Data);
        Assert.Contains(missing.Errors, e => e.Contains("file not found") && e.Contains(Path.Combine(Home, "conf", "none.pfx")));

        TestCertificates.WritePfx(Conf("site.pfx"), null);
        var found = LoadIni(Https + "[tls]\ncert_file = conf/site.pfx\n" + Data);

        Assert.True(found.IsValid, string.Join("; ", found.Errors));
        Assert.Equal(Conf("site.pfx"), found.Settings.CertFilePath);
    }

    [Fact]
    public void Https_CertificateDirectory_OrKeyWithoutCertificate_IsAnError()
    {
        Directory.CreateDirectory(Conf("adir"));
        var asDirectory = LoadIni(Https + "[tls]\ncert_file = conf/adir\n" + Data);
        Assert.Contains(asDirectory.Errors, e => e.Contains("is a directory"));

        File.WriteAllText(Conf("k.key"), "x");
        var keyOnly = LoadIni(Https + "[tls]\ncert_key_file = conf/k.key\n" + Data);
        Assert.Contains(keyOnly.Errors, e => e.Contains("cert_key_file: set without cert_file"));
    }

    [Fact]
    public void Https_MissingKeyFile_IsAnError()
    {
        File.WriteAllText(Conf("c.pem"), "x");
        var report = LoadIni(Https + "[tls]\ncert_file = conf/c.pem\ncert_key_file = conf/none.key\n" + Data);

        Assert.Contains(report.Errors, e => e.StartsWith("[tls] cert_key_file: file not found"));
    }

    [Fact]
    public void CertificateInsidePublic_IsRefused_SoItIsNeverServed()
    {
        Directory.CreateDirectory(Path.Combine(Home, "public"));
        TestCertificates.WritePfx(Path.Combine(Home, "public", "site.pfx"), null);

        var report = LoadIni(Https + "[tls]\ncert_file = public/site.pfx\n" + Data + "public = public\n");

        Assert.Contains(report.Errors, e => e.Contains("would be served to browsers"));
    }

    [Fact]
    public void PemLookingFileWithoutKey_GivesAWarning()
    {
        File.WriteAllText(Conf("c.pem"), "x");
        var report = LoadIni(Https + "[tls]\ncert_file = conf/c.pem\n" + Data);

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Contains(report.Warnings, w => w.Contains("looks like a PEM file"));
    }

    [Fact]
    public void TlsSettingsWithHttp_AreIgnoredWithAWarning_AndTheirFilesAreNotRequired()
    {
        var report = LoadIni("[server]\nprotocol = http\n[tls]\ncert_file = conf/none.pfx\n" + Data);

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        Assert.Contains(report.Warnings, w => w.Contains("[tls] settings are present but [server] protocol is http"));
    }
}
