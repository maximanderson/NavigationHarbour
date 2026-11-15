using Microsoft.Extensions.Configuration;
using PortalCore.Api.Configuration.Server;

namespace PortalCore.Api.Tests;

/// <summary>Stage 4: runtime certificate loading (PFX, PEM + key, encrypted key) and its failures.</summary>
public class TlsLoaderTests : TlsTestBase
{
    private const string Password = "S3cret-Pa55-word!";
    private string Conf(string file) => Path.Combine(Home, "conf", file);

    private ServerSettings Settings(string tls)
    {
        var report = LoadIni("[server]\nprotocol = https\n[tls]\n" + tls + "[paths]\ndata = data\n");
        Assert.True(report.IsValid, string.Join("; ", report.Errors));
        return report.Settings;
    }

    [Fact]
    public void Pfx_WithPassword_Loads()
    {
        TestCertificates.WritePfx(Conf("s.pfx"), Password);

        using var cert = TlsCertificateLoader.Load(Settings($"cert_file = conf/s.pfx\ncert_password = {Password}\n"));

        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public void Pfx_WithoutPassword_Loads()
    {
        TestCertificates.WritePfx(Conf("s.pfx"), null);

        using var cert = TlsCertificateLoader.Load(Settings("cert_file = conf/s.pfx\n"));

        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public void Pem_WithSeparateKey_Loads()
    {
        TestCertificates.WritePem(Path.Combine(Home, "conf"), "s", null);

        using var cert = TlsCertificateLoader.Load(Settings("cert_file = conf/s.pem\ncert_key_file = conf/s.key\n"));

        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public void Pem_WithEncryptedKey_Loads()
    {
        TestCertificates.WritePem(Path.Combine(Home, "conf"), "s", Password);

        using var cert = TlsCertificateLoader.Load(
            Settings($"cert_file = conf/s.pem\ncert_key_file = conf/s.key\ncert_password = {Password}\n"));

        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public void PasswordFromAnEnvironmentStyleSource_IsUsed()
    {
        TestCertificates.WritePfx(Conf("s.pfx"), Password);
        WriteIni("[server]\nprotocol = https\n[tls]\ncert_file = conf/s.pfx\n[paths]\ndata = data\n");
        var config = WithShippedDefaults();
        config.AddInMemoryCollection(new Dictionary<string, string?> { ["tls:cert_password"] = Password });

        var report = Load(config);
        using var cert = TlsCertificateLoader.Load(report.Settings);

        Assert.True(cert.HasPrivateKey);
        Assert.Equal("********", report.Values.Single(v => v.Key == "cert_password").Value);
    }

    [Fact]
    public void Pfx_MissingOrWrongPassword_IsRefused_WithoutRevealingAnyPassword()
    {
        TestCertificates.WritePfx(Conf("s.pfx"), Password);

        var missing = Assert.Throws<TlsCertificateException>(() => TlsCertificateLoader.Load(Settings("cert_file = conf/s.pfx\n")));
        Assert.Contains("needs a password", missing.Message);

        var wrong = Assert.Throws<TlsCertificateException>(
            () => TlsCertificateLoader.Load(Settings("cert_file = conf/s.pfx\ncert_password = another-wrong-password\n")));
        Assert.Contains("password is wrong", wrong.Message);
        Assert.DoesNotContain("another-wrong-password", wrong.ToString());
        Assert.DoesNotContain(Password, wrong.ToString());
    }

    [Fact]
    public void Pem_EncryptedKey_MissingOrWrongPassword_IsRefused()
    {
        TestCertificates.WritePem(Path.Combine(Home, "conf"), "s", Password);

        Assert.Throws<TlsCertificateException>(
            () => TlsCertificateLoader.Load(Settings("cert_file = conf/s.pem\ncert_key_file = conf/s.key\n")));
        var wrong = Assert.Throws<TlsCertificateException>(
            () => TlsCertificateLoader.Load(Settings("cert_file = conf/s.pem\ncert_key_file = conf/s.key\ncert_password = nope-nope\n")));

        Assert.DoesNotContain("nope-nope", wrong.ToString());
    }

    [Fact]
    public void GarbageFile_IsRefused()
    {
        File.WriteAllText(Conf("s.pfx"), "this is not a certificate");

        var ex = Assert.Throws<TlsCertificateException>(() => TlsCertificateLoader.Load(Settings("cert_file = conf/s.pfx\n")));

        Assert.Contains("not a valid PFX", ex.Message);
    }

    [Fact]
    public void Pem_KeyThatDoesNotMatchTheCertificate_IsRefused()
    {
        TestCertificates.WritePem(Path.Combine(Home, "conf"), "a", null);
        TestCertificates.WritePem(Path.Combine(Home, "conf"), "b", null);

        Assert.Throws<TlsCertificateException>(
            () => TlsCertificateLoader.Load(Settings("cert_file = conf/a.pem\ncert_key_file = conf/b.key\n")));
    }

    [Fact]
    public void ExpiredCertificate_IsRefused()
    {
        using var expired = TestCertificates.Create(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1));
        TestCertificates.WritePfx(Conf("s.pfx"), null, expired);

        var ex = Assert.Throws<TlsCertificateException>(() => TlsCertificateLoader.Load(Settings("cert_file = conf/s.pfx\n")));

        Assert.Contains("expired", ex.Message);
    }

    [Fact]
    public void CertificatePaths_AreResolvedAgainstHome_NotTheWorkingDirectory()
    {
        TestCertificates.WritePfx(Conf("s.pfx"), null);
        var settings = Settings("cert_file = conf/s.pfx\n");

        // The loader receives an absolute path under Home, so the process working directory cannot matter.
        Assert.True(Path.IsPathRooted(settings.CertFilePath!));
        Assert.StartsWith(Home, settings.CertFilePath);
        Assert.NotEqual(Directory.GetCurrentDirectory(), Path.GetDirectoryName(Path.GetDirectoryName(settings.CertFilePath)));

        using var cert = TlsCertificateLoader.Load(settings);
        Assert.True(cert.HasPrivateKey);
    }
}
