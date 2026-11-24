using Microsoft.Extensions.Configuration;
using Portal.Api.Configuration.Server;

namespace Portal.Api.Tests;

/// <summary>Creates a scratch home directory per test and loads configuration against it.</summary>
public abstract class ServerConfigTestBase : IDisposable
{
    protected string Home { get; } =
        Path.Combine(Path.GetTempPath(), "portal-tests-" + Guid.NewGuid().ToString("N"));

    protected ServerConfigTestBase()
    {
        Directory.CreateDirectory(Path.Combine(Home, "conf"));
    }

    protected void WriteIni(string content) =>
        File.WriteAllText(Path.Combine(Home, "conf", "server.ini"), content);

    /// <summary>Stands in for the host content root (the working directory when run from source).</summary>
    protected string ContentRoot => Path.Combine(Home, "content-root");

    protected ServerConfigReport Load(string environment = "Production", params string[] args) =>
        Load(new ConfigurationManager(), environment, args);

    protected ServerConfigReport Load(ConfigurationManager config, string environment = "Production", params string[] args)
    {
        var startup = StartupOptions.Parse(args.Length == 0 ? new[] { "--home", Home } : args);
        return ServerConfiguration.Load(config, startup, environment, AppContext.BaseDirectory, null, ContentRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(Home))
        {
            Directory.Delete(Home, recursive: true);
        }
    }
}
