using System.Net;

namespace PortalCore.Api.Configuration.Server;

public static partial class ServerConfiguration
{
    private static string? CheckPath(
        ServerSchema.Setting setting,
        string raw,
        string? homePath,
        Action<string> fail)
    {
        if (raw.Length == 0)
        {
            fail("must not be empty; give a directory name or an absolute path.");
            return null;
        }

        if (raw.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || HasInlineComment(raw))
        {
            fail($"{Quote(setting, raw)} is not a valid path.{CommentHint(raw)}");
            return null;
        }

        try
        {
            if (homePath is null)
            {
                return Path.IsPathRooted(raw) ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw)) : null;
            }

            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw, homePath));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            fail($"{Quote(setting, raw)} is not a valid path.");
            return null;
        }
    }

    private static string? CheckRootUrl(string raw, out Uri? url)
    {
        url = null;

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            // The value is not echoed: it may carry credentials.
            return "must be an absolute http:// or https:// URL, for example https://portalcore.example.com:3001/.";
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            return "must not contain a user name or password.";
        }

        if (!string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment))
        {
            return "must not contain a query string or fragment.";
        }

        if (parsed.AbsolutePath != "/")
        {
            return "must not contain a path; Portal Core is served from the root of the host (sub-path hosting is not supported).";
        }

        url = parsed;
        return null;
    }

    internal static bool IsValidListenAddress(string value)
    {
        if (string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // IPAddress.TryParse accepts shorthand such as "1" (= 0.0.0.1); require full notation.
        var looksIpv6 = value.Contains(':');
        if (!looksIpv6 && value.Count(c => c == '.') != 3)
        {
            return false;
        }

        return IPAddress.TryParse(value, out var address) &&
               (!looksIpv6 || address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
    }

    internal static bool IsValidHostName(string value) =>
        value.Length > 0 &&
        value.IndexOfAny(new[] { ' ', '/', '\\', '@', ';', '#' }) < 0 &&
        Uri.CheckHostName(value) != UriHostNameType.Unknown;

    private static bool IsNonLoopbackListener(string address)
    {
        if (address.Length == 0)
        {
            return true;
        }

        if (string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IPAddress.TryParse(address, out var ip) && !IPAddress.IsLoopback(ip);
    }

    private static bool IsUnderOrEqual(string path, string root) => PortalPaths.IsSameOrUnder(path, root);

    private static bool HasInlineComment(string raw) => raw.Contains(" ;") || raw.Contains(" #");
}
