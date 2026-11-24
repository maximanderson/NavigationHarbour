using System.Net;

namespace PortalCore.Api.Configuration.Server;

/// <summary>
/// Validation of [proxy]: the reverse proxies whose X-Forwarded-For header Portal Core may trust.
/// Empty values keep the framework default (loopback only). Nothing here can widen trust to "everyone":
/// a network with prefix length 0 is refused.
/// </summary>
public static partial class ServerConfiguration
{
    /// <summary>Validates a [proxy] value. Always returns null (no resolved path).</summary>
    private static string? ValidateProxy(ServerSchema.Setting setting, string raw, ServerSettings settings, Action<string> fail)
    {
        var isNetworks = setting.Key == "trusted_networks";
        var addresses = new List<IPAddress>();
        var networks = new List<IPNetwork>();

        if (raw.Length > 0)
        {
            foreach (var part in raw.Split(','))
            {
                var entry = part.Trim();

                if (entry.Length == 0)
                {
                    fail($"{Quote(setting, raw)} contains an empty entry; separate entries with a single comma.{CommentHint(raw)}");
                    continue;
                }

                if (isNetworks)
                {
                    var problem = CheckNetwork(entry, out var network);
                    if (problem is null) networks.Add(network); else fail($"\"{entry}\" {problem}{CommentHint(raw)}");
                }
                else if (TryParseProxyAddress(entry, out var address))
                {
                    addresses.Add(address);
                }
                else
                {
                    fail($"\"{entry}\" is not a valid IP address; use full notation such as 10.0.0.5 or fd00::5 " +
                         $"(for a range use trusted_networks).{CommentHint(raw)}");
                }
            }
        }

        if (isNetworks) settings.TrustedNetworks = networks; else settings.TrustedProxies = addresses;
        return null;
    }

    private static bool TryParseProxyAddress(string value, out IPAddress address)
    {
        address = IPAddress.None;

        // Same strictness as [server] http_addr: no shorthand such as "1", no host names, no "/prefix".
        var looksIpv6 = value.Contains(':');
        if (!looksIpv6 && value.Count(c => c == '.') != 3)
        {
            return false;
        }

        if (!IPAddress.TryParse(value, out var parsed) ||
            (looksIpv6 && parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6))
        {
            return false;
        }

        address = parsed;
        return true;
    }

    private static string? CheckNetwork(string value, out IPNetwork network)
    {
        network = default;

        var slash = value.IndexOf('/');
        if (slash <= 0 || slash != value.LastIndexOf('/') || !TryParseProxyAddress(value[..slash], out _))
        {
            return "is not a valid network; use CIDR notation such as 10.0.0.0/24 or fd00::/64.";
        }

        // IPNetwork.TryParse silently clears host bits (10.0.0.5/24 becomes 10.0.0.0/24); require the exact network address.
        if (!IPNetwork.TryParse(value, out network) || !network.BaseAddress.Equals(IPAddress.Parse(value[..slash])))
        {
            return "is not a valid network; the prefix length is out of range or the address has bits set " +
                   "beyond the prefix (for example 10.0.0.5/24 should be 10.0.0.0/24).";
        }

        if (network.PrefixLength == 0)
        {
            return "would trust every address; use a narrower network. Trusting everyone would allow client IP spoofing.";
        }

        return null;
    }
}
