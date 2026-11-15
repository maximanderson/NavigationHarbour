namespace PortalCore.Api.Configuration.Server;

/// <summary>Path resolution against the Portal Core home directory (never the working directory).</summary>
public static class PortalPaths
{
    /// <summary>
    /// Returns an absolute path for a relative one, resolved against <paramref name="home"/>.
    /// Absolute and empty paths, or an unknown home, are returned unchanged.
    /// Pure computation: nothing is created or touched on disk.
    /// </summary>
    public static string ResolveAgainstHome(string? home, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(home) || Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim(), home));
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="root"/> or lies below it (pure string check).</summary>
    public static bool IsSameOrUnder(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootWithSeparator = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return (Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar).StartsWith(rootWithSeparator, comparison);
    }

    /// <summary>True when the two directories are the same or one contains the other.</summary>
    public static bool Overlaps(string a, string b) => IsSameOrUnder(a, b) || IsSameOrUnder(b, a);
}
