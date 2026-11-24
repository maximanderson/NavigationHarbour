namespace Portal.Api.Configuration.Server;

/// <summary>One effective setting, ready to display.</summary>
/// <param name="Value">Display value (secrets and credentials already redacted).</param>
/// <param name="Source">Where the value came from: default, server.ini, environment variable or command line.</param>
/// <param name="ResolvedPath">For [paths] settings: the absolute path after resolving against the home directory.</param>
/// <param name="Applied">True when the setting is applied to the running application; false when validated only.</param>
public sealed record SettingView(string Section, string Key, string Value, string Source, string? ResolvedPath, bool Applied = false);

/// <summary>Outcome of loading and validating the private server configuration.</summary>
public sealed class ServerConfigReport
{
    public required PortalHomeResult Home { get; init; }

    /// <summary>ASP.NET Core content root: shipped appsettings*.json and application content.</summary>
    public string? ContentRoot { get; init; }

    /// <summary>Expected location of server.ini (null if the home directory is unknown).</summary>
    public string? ConfigPath { get; init; }

    public bool FileFound { get; init; }
    public required string EnvironmentName { get; init; }

    /// <summary>The one effective database location (Stage 3A). Set by Load; never opened with SQLite.</summary>
    public DatabaseLocation? Database { get; set; }

    public ServerSettings Settings { get; } = new();
    public List<SettingView> Values { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Notices { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
