using Microsoft.Extensions.Configuration;

namespace Portal.Api.Configuration.Server;

/// <summary>
/// Reads server.ini on its own (without touching the application configuration) so that a
/// malformed or unknown-key file can be reported clearly before it is merged.
/// ASP.NET Core's standard INI provider does the parsing. Only key *names* are kept here,
/// never values, so file contents cannot leak into messages.
/// </summary>
public sealed class ServerIniFile
{
    public required string Path { get; init; }
    public bool Exists { get; private set; }

    /// <summary>Full configuration keys found in the file, e.g. "server:port".</summary>
    public List<string> Keys { get; } = new();

    /// <summary>Syntax or access problems. When not empty the file must not be loaded.</summary>
    public List<string> Errors { get; } = new();

    public bool IsUsable => Exists && Errors.Count == 0;

    public static ServerIniFile Read(string path)
    {
        var file = new ServerIniFile { Path = path };

        if (Directory.Exists(path))
        {
            file.Exists = true;
            file.Errors.Add($"{path} is a directory; expected a file.");
            return file;
        }

        if (!File.Exists(path))
        {
            return file;
        }

        file.Exists = true;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (UnauthorizedAccessException)
        {
            file.Errors.Add($"Access denied reading {path}. The {ProductInfo.Name} account needs Read permission on the conf directory.");
            return file;
        }
        catch (IOException ex)
        {
            file.Errors.Add($"Cannot read {path}: {ex.GetType().Name}.");
            return file;
        }

        // Line-level check first: the provider's own message would quote the offending line,
        // which could be a secret. Report only the line number.
        for (var i = 0; i < lines.Length; i++)
        {
            var problem = CheckLine(lines[i]);
            if (problem is not null)
            {
                file.Errors.Add($"{path} line {i + 1}: {problem}");
            }
        }

        if (file.Errors.Count > 0)
        {
            return file;
        }

        try
        {
            var parsed = new ConfigurationBuilder().AddIniFile(path, optional: false, reloadOnChange: false).Build();
            file.Keys.AddRange(parsed.AsEnumerable().Where(pair => pair.Value is not null).Select(pair => pair.Key));
        }
        catch (Exception ex)
        {
            file.Errors.Add($"{path} could not be parsed: {Describe(ex)}");
        }

        return file;
    }

    private static string? CheckLine(string raw)
    {
        var line = raw.Trim();

        if (line.Length == 0 || line[0] is ';' or '#' or '/')
        {
            return null;
        }

        if (line[0] == '[')
        {
            if (line[^1] != ']')
            {
                return "section header is missing the closing ']'.";
            }

            return line.Length > 2 ? null : "section header has no name.";
        }

        var separator = line.IndexOf('=');
        if (separator < 0)
        {
            return "expected a comment (';'), a [section] or 'key = value'.";
        }

        return line[..separator].Trim().Length == 0 ? "setting has an empty key." : null;
    }

    private static string Describe(Exception ex)
    {
        // Duplicate-key messages name only the key, so they are safe to show.
        for (var current = ex; current is not null; current = current.InnerException!)
        {
            if (current.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return current.Message;
            }

            if (current.InnerException is null)
            {
                break;
            }
        }

        return ex.GetType().Name + ".";
    }
}
