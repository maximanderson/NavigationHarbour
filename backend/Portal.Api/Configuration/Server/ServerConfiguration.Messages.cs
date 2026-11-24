namespace Portal.Api.Configuration.Server;

public static partial class ServerConfiguration
{
    private static string CommentHint(string raw) =>
        raw.Contains(';') || raw.Contains('#')
            ? " Comments must be on their own line; text after a value is part of the value."
            : string.Empty;

    /// <summary>Quotes a value for an error message. Secret settings are never echoed.</summary>
    private static string Quote(ServerSchema.Setting setting, string raw) =>
        ServerSchema.IsSecret(setting.Key) ? "(hidden)" : $"\"{raw}\"";

    /// <summary>Value as shown by --print-config.</summary>
    private static string Display(ServerSchema.Setting setting, string raw)
    {
        if (ServerSchema.IsSecret(setting.Key))
        {
            return raw.Length == 0 ? "(empty)" : "********";
        }

        if (raw.Length == 0)
        {
            return "(empty)";
        }

        // URLs may carry credentials ("https://user:pass@host"); never print those.
        return raw.Contains('@') && raw.Contains("://") ? "********" : raw;
    }

    private static string Hint(string value, IEnumerable<string> candidates)
    {
        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in candidates)
        {
            var distance = EditDistance(value.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best is not null && bestDistance <= 2 ? $"; did you mean \"{best}\"?" : ".";
    }

    private static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
