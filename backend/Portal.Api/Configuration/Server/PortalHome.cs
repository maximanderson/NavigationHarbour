namespace Portal.Api.Configuration.Server;

/// <summary>
/// Result of locating the Navigation Harbour home directory.
/// <see cref="Path"/> is null when the home could not be determined.
/// <see cref="Problem"/> describes why (or why an explicit home is unusable).
/// </summary>
public sealed record PortalHomeResult(string? Path, string Source, string? Problem)
{
    public const string UnresolvedSource = "unresolved";

    /// <summary>True when no explicit or discoverable home exists (nothing was asked for).</summary>
    public bool IsUnresolved => Source == UnresolvedSource;
}

/// <summary>
/// Locates the Navigation Harbour home directory independently of the current working directory.
/// Order: --home, PORTAL_HOME (or the legacy PORTALCORE_HOME), installation layout (bin/ next to conf|public|data),
/// then the development project directory (found by walking up from the binaries).
/// </summary>
public static class PortalHome
{
    public const string EnvironmentVariable = "PORTAL_HOME";

    /// <summary>Name used before the product was renamed; still honoured, but EnvironmentVariable wins.</summary>
    public const string LegacyEnvironmentVariable = "PORTALCORE_HOME";
    public const string ProjectFileName = "Portal.Api.csproj";

    private static readonly string[] LayoutFolders = { "conf", "public", "data" };

    /// <summary>
    /// Reads the home from the environment: PORTAL_HOME first, then the legacy PORTALCORE_HOME.
    /// A blank value counts as not set. Returns the value and the name of the variable it came from.
    /// </summary>
    public static (string? Value, string Name) ReadEnvironment(Func<string, string?> getVariable)
    {
        var value = getVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return (value, EnvironmentVariable);
        }

        value = getVariable(LegacyEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(value)
            ? (value, LegacyEnvironmentVariable)
            : (null, EnvironmentVariable);
    }

    public static PortalHomeResult Resolve(
        string? explicitHome,
        string? environmentHome,
        string baseDirectory,
        string environmentVariableName = EnvironmentVariable)
    {
        if (!string.IsNullOrWhiteSpace(explicitHome))
        {
            return FromExplicit(explicitHome, "--home argument");
        }

        if (!string.IsNullOrWhiteSpace(environmentHome))
        {
            return FromExplicit(environmentHome, $"{environmentVariableName} environment variable");
        }

        var installHome = FromInstallLayout(baseDirectory);
        if (installHome is not null)
        {
            return new PortalHomeResult(installHome, "installation layout", null);
        }

        var projectHome = FromProjectDirectory(baseDirectory);
        if (projectHome is not null)
        {
            return new PortalHomeResult(projectHome, "development project directory", null);
        }

        return new PortalHomeResult(
            null,
            PortalHomeResult.UnresolvedSource,
            $"Cannot determine the {ProductInfo.Name} home directory. Use --home <directory> or set {EnvironmentVariable}.");
    }

    /// <summary>
    /// ASP.NET Core content root: where the shipped appsettings*.json and application content live.
    /// Separate from Navigation Harbour home. Never derived from the working directory.
    ///   Development (project found above the build output): the project directory.
    ///   Installation (executable in a "bin" directory): that bin directory.
    ///   Anything else: the directory containing the application.
    /// Independent of --home and PORTAL_HOME (or the legacy PORTALCORE_HOME), which only move Navigation Harbour home.
    /// </summary>
    public static string ResolveContentRoot(string baseDirectory)
    {
        var applicationDirectory = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(baseDirectory));

        return FromProjectDirectory(applicationDirectory) ?? applicationDirectory;
    }

    private static PortalHomeResult FromExplicit(string value, string source)
    {
        string full;
        try
        {
            full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(value.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or System.IO.PathTooLongException)
        {
            return new PortalHomeResult(null, source, $"Home directory from {source} is not a valid path.");
        }

        return Directory.Exists(full)
            ? new PortalHomeResult(full, source, null)
            : new PortalHomeResult(full, source, $"Home directory from {source} does not exist: {full}");
    }

    /// <summary>Published layout: &lt;home&gt;/bin/NavigationHarbour.dll with conf, public or data beside bin.</summary>
    private static string? FromInstallLayout(string baseDirectory)
    {
        var binDirectory = System.IO.Path.TrimEndingDirectorySeparator(baseDirectory);

        if (!string.Equals(System.IO.Path.GetFileName(binDirectory), "bin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parent = System.IO.Path.GetDirectoryName(binDirectory);
        if (string.IsNullOrEmpty(parent))
        {
            return null;
        }

        return LayoutFolders.Any(folder => Directory.Exists(System.IO.Path.Combine(parent, folder)))
            ? parent
            : null;
    }

    /// <summary>Running from source: bin/Debug/net10.0 sits below the project directory.</summary>
    private static string? FromProjectDirectory(string baseDirectory)
    {
        var directory = new DirectoryInfo(baseDirectory);

        for (var depth = 0; depth < 10 && directory is not null; depth++, directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, ProjectFileName)))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
