using Microsoft.Extensions.Configuration;
using PortalCore.Api.Configuration.Server;

namespace PortalCore.Api.Tests;

/// <summary>Stage 4: --check-config / --print-config output and secret redaction.</summary>
public class TlsRedactionTests : TlsTestBase
{
    private const string Password = "Zx9-very-secret-pw";
    private string Conf(string file) => Path.Combine(Home, "conf", file);

    private (int Exit, string Text) Run(bool print, ServerConfigReport report)
    {
        var args = new[] { print ? "--print-config" : "--check-config", "--home", Home };
        var writer = new StringWriter();
        var exit = ServerConfigCommands.Run(StartupOptions.Parse(args), report, writer);
        return (exit, writer.ToString());
    }

    private string Everything(ServerConfigReport report)
    {
        var (_, printed) = Run(print: true, report);
        var (_, checkedText) = Run(print: false, report);
        return printed + checkedText + string.Join("\n", report.Errors.Concat(report.Warnings).Concat(report.Notices)) +
               string.Join("\n", report.Values.Select(v => v.Value + v.ResolvedPath));
    }

    [Fact]
    public void Password_NeverAppearsInPrintCheckOrMessages()
    {
        TestCertificates.WritePfx(Conf("s.pfx"), Password);
        var report = LoadIni($"[server]\nprotocol = https\n[tls]\ncert_file = conf/s.pfx\ncert_password = {Password}\n[paths]\ndata = data\n");

        var text = Everything(report);

        Assert.DoesNotContain(Password, text);
        Assert.Contains("cert_password", text);
        Assert.Contains("********", text);
        Assert.Contains(Conf("s.pfx"), text);
    }

    [Fact]
    public void Password_IsNotLeaked_EvenWhenTheConfigurationIsInvalid()
    {
        var report = LoadIni($"[server]\nprotocol = https\nport = nope\n[tls]\ncert_file = conf/missing.pfx\ncert_password = {Password}\n[paths]\ndata = data\n");

        Assert.False(report.IsValid);
        Assert.DoesNotContain(Password, Everything(report));

        var failure = new StringWriter();
        ServerConfigCommands.WriteStartupFailure(report, failure);
        Assert.DoesNotContain(Password, failure.ToString());
    }

    [Fact]
    public void PasswordFromAnotherSource_IsAlsoRedacted_AndWinsOverServerIni()
    {
        TestCertificates.WritePfx(Conf("s.pfx"), Password);
        WriteIni("[server]\nprotocol = https\n[tls]\ncert_file = conf/s.pfx\ncert_password = wrong-in-ini\n[paths]\ndata = data\n");
        var config = WithShippedDefaults();
        config.AddInMemoryCollection(new Dictionary<string, string?> { ["tls:cert_password"] = Password });

        var report = Load(config);

        Assert.Equal(Password, report.Settings.CertPassword);
        var text = Everything(report);
        Assert.DoesNotContain(Password, text);
        Assert.DoesNotContain("wrong-in-ini", text);
    }

    [Fact]
    public void KeyFilePath_IsShown_NotRedacted()
    {
        File.WriteAllText(Conf("c.pem"), "x");
        File.WriteAllText(Conf("c.key"), "x");
        var report = LoadIni("[server]\nprotocol = https\n[tls]\ncert_file = conf/c.pem\ncert_key_file = conf/c.key\n[paths]\ndata = data\n");

        var (_, text) = Run(print: true, report);

        Assert.Contains("cert_key_file", text);
        Assert.Contains(Conf("c.key"), text);
    }

    [Fact]
    public void Diagnostics_ValidateFilesButNeverLoadCertificates_OrCreateFiles()
    {
        // A file that exists but is not a certificate passes configuration validation: loading is a startup step.
        File.WriteAllText(Conf("s.pfx"), "not a certificate");
        Directory.CreateDirectory(Path.Combine(Home, "data"));
        using (var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(Home, "data", "portalcore.db")};Pooling=False"))
        {
            db.Open();
            using var create = db.CreateCommand();
            create.CommandText = "CREATE TABLE UsageLogs (Id INTEGER PRIMARY KEY);";
            create.ExecuteNonQuery();
        }
        var report = LoadIni("[server]\nprotocol = https\n[tls]\ncert_file = conf/s.pfx\n[paths]\ndata = data\n");
        var before = Directory.GetFileSystemEntries(Home, "*", SearchOption.AllDirectories).Order().ToArray();

        var (exit, text) = Run(print: false, report);

        Assert.True(exit == 0, text);
        Assert.Contains("Result: OK", text);
        Assert.Equal(before, Directory.GetFileSystemEntries(Home, "*", SearchOption.AllDirectories).Order().ToArray());
    }
}
