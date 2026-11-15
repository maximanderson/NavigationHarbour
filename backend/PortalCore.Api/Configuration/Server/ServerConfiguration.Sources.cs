using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Ini;
using Microsoft.Extensions.Configuration.Json;

namespace PortalCore.Api.Configuration.Server;

public static partial class ServerConfiguration
{
    private static void InsertAfterAppSettings(ConfigurationManager config, IConfigurationSource source)
    {
        // appsettings*.json are JsonConfigurationSource; user secrets are a different type and stay above.
        var index = -1;
        for (var i = 0; i < config.Sources.Count; i++)
        {
            if (config.Sources[i] is JsonConfigurationSource)
            {
                index = i;
            }
        }

        config.Sources.Insert(index + 1, source);
    }

    private static void InsertBeforeCommandLine(ConfigurationManager config, IConfigurationSource source)
    {
        var index = config.Sources.Count;
        for (var i = 0; i < config.Sources.Count; i++)
        {
            if (config.Sources[i] is CommandLineConfigurationSource)
            {
                index = i;
                break;
            }
        }

        config.Sources.Insert(index, source);
    }

    private static void CheckIniSections(ServerIniFile ini, ServerConfigReport report)
    {
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in ini.Keys)
        {
            var parts = key.Split(':');

            if (parts.Length == 1)
            {
                if (reported.Add(key))
                {
                    report.Errors.Add($"{ConfigFileName}: setting \"{key}\" is outside any [section].");
                }
                continue;
            }

            var section = parts[0];
            if (!ServerSchema.Sections.Contains(section, StringComparer.OrdinalIgnoreCase) && reported.Add(section))
            {
                report.Errors.Add($"{ConfigFileName}: unknown section [{section}]{Hint(section, ServerSchema.Sections)}");
            }
        }
    }

    private static string SourceOf(IConfigurationRoot root, string key)
    {
        foreach (var provider in root.Providers.Reverse())
        {
            if (!provider.TryGet(key, out _))
            {
                continue;
            }

            return provider switch
            {
                IniConfigurationProvider => ConfigFileName,
                CommandLineConfigurationProvider => "command line",
                EnvironmentVariablesConfigurationProvider env when env.ToString()!.Contains(EnvironmentPrefix)
                    => $"environment variable ({EnvironmentPrefix} prefix)",
                EnvironmentVariablesConfigurationProvider => "environment variable",
                FileConfigurationProvider file => Path.GetFileName(file.Source.Path ?? "configuration file"),
                _ => "other configuration source",
            };
        }

        return "default";
    }
}
