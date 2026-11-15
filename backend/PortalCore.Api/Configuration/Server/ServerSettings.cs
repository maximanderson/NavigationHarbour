namespace PortalCore.Api.Configuration.Server;

/// <summary>
/// The closed schema of server.ini: every section and key that is allowed, with its default.
/// Anything else is rejected by the validator.
/// </summary>
public static class ServerSchema
{
    public sealed record Setting(string Section, string Key, string? Default)
    {
        /// <summary>Configuration key as used by the ASP.NET Core provider ("server:port").</summary>
        public string ConfigKey => $"{Section}:{Key}";
    }

    public static readonly IReadOnlyList<Setting> Settings = new Setting[]
    {
        new("server", "protocol", "http"),
        new("server", "http_addr", "127.0.0.1"),
        new("server", "domain", "localhost"),
        new("server", "port", "5247"),
        new("server", "root_url", null),

        // Certificate for protocol = https (Stage 4). Files are relative to Portal Core home or absolute.
        // cert_file alone is a PFX; with cert_key_file it is a PEM certificate and its private key.
        // cert_password opens the PFX, or an encrypted PEM private key. It is a secret.
        new("tls", "cert_file", null),
        new("tls", "cert_key_file", null),
        new("tls", "cert_password", null),

        new("paths", "data", "data"),
        new("paths", "logs", "logs"),
        new("paths", "public", "public"),
        // Database backups (Stage 3B.1). Never served over HTTP; created by the first backup, not by diagnostics.
        new("paths", "backups", "backups"),
        // No default on purpose: unset keeps the existing AiSettings folders (Data/knowledge,
        // Data/prompts). When set, knowledge is <ai>/knowledge and prompts are <ai>/prompts.
        new("paths", "ai", null),

        new("log", "level", "info"),
        new("log", "console", "true"),
        new("log", "file", "true"),
        new("log", "max_file_size_mb", "10"),
        new("log", "retain_days", "14"),
    };

    public static IReadOnlyList<string> Sections { get; } =
        Settings.Select(s => s.Section).Distinct().ToList();

    public static IEnumerable<string> KeysOf(string section) =>
        Settings.Where(s => string.Equals(s.Section, section, StringComparison.OrdinalIgnoreCase)).Select(s => s.Key);

    public static Setting? Find(string section, string key) =>
        Settings.FirstOrDefault(s =>
            string.Equals(s.Section, section, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when the setting can be applied to the running application (otherwise validated only).
    /// [server] protocol/http_addr/port and [tls] are applied to Kestrel only while the listening
    /// endpoint is explicitly configured (and [tls] only for https); the validator narrows this down.
    /// </summary>
    public static bool IsApplied(string section, string key) =>
        string.Equals(section, "log", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(section, "tls", StringComparison.OrdinalIgnoreCase) ||
        (string.Equals(section, "server", StringComparison.OrdinalIgnoreCase) &&
         (string.Equals(key, "protocol", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "http_addr", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "port", StringComparison.OrdinalIgnoreCase))) ||
        (string.Equals(section, "paths", StringComparison.OrdinalIgnoreCase) &&
         (string.Equals(key, "logs", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "data", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "backups", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(key, "ai", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Keys whose values must never be printed or quoted in messages. A key is secret when one of its
    /// words (split at '_', '-', '.', ':') is password, passphrase, secret or token, or when it is "key"
    /// or ends in "_key" (api_key, private_key). A file name such as "cert_key_file" is a path, not a secret.
    /// </summary>
    public static bool IsSecret(string key)
    {
        var normalized = key.ToLowerInvariant().Replace('-', '_').Replace('.', '_').Replace(':', '_');

        return normalized.Split('_').Any(word => word is "password" or "passphrase" or "secret" or "token" or "apikey") ||
               normalized == "key" ||
               normalized.EndsWith("_key", StringComparison.Ordinal);
    }
}

/// <summary>
/// Typed, validated view of server.ini. [log], [paths] logs and [paths] ai are applied to the
/// running application through <see cref="ServerConfiguration"/>; the rest is validated only.
/// The listening endpoint and certificate (Stage 4) are applied by KestrelSetup.
/// </summary>
public sealed class ServerSettings
{
    // [server]
    public string Protocol { get; set; } = "http";
    public string HttpAddr { get; set; } = "127.0.0.1";
    public string Domain { get; set; } = "localhost";
    public int Port { get; set; } = 5247;
    public Uri? RootUrl { get; set; }

    /// <summary>
    /// True when protocol, http_addr or port is configured by any source (server.ini, environment,
    /// command line). Only then does Portal Core configure Kestrel itself; otherwise the existing
    /// ASPNETCORE_URLS / launchSettings.json behaviour is untouched.
    /// </summary>
    public bool ExplicitEndpoint { get; set; }

    /// <summary>Endpoint-related warnings that the running server also writes to its log (never contain secrets).</summary>
    public List<string> EndpointWarnings { get; } = new();

    // [tls]: as written, and the files resolved against the home directory. CertPassword is a secret:
    // it is never printed, logged or placed in a message.
    public string? CertFile { get; set; }
    public string? CertKeyFile { get; set; }
    public string? CertPassword { get; set; }
    public string? CertFilePath { get; set; }
    public string? CertKeyFilePath { get; set; }

    // [paths] as written in the configuration
    public string Data { get; set; } = "data";
    public string Logs { get; set; } = "logs";
    public string Public { get; set; } = "public";
    public string? Ai { get; set; }
    public string Backups { get; set; } = "backups";

    // [paths] resolved against the home directory (null when it could not be resolved)
    public string? DataPath { get; set; }
    public string? LogsPath { get; set; }
    public string? PublicPath { get; set; }
    public string? AiPath { get; set; }
    public string? BackupsPath { get; set; }

    // [log]
    public string LogLevel { get; set; } = "info";
    public bool LogConsole { get; set; } = true;
    public bool LogFile { get; set; } = true;
    public int MaxFileSizeMb { get; set; } = 10;
    public int RetainDays { get; set; } = 14;
}
