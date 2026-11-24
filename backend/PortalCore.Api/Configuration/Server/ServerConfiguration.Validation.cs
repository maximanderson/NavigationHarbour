using Microsoft.Extensions.Configuration;

namespace PortalCore.Api.Configuration.Server;

public static partial class ServerConfiguration
{
    /// <summary>Validates the effective (merged) values and fills the report.</summary>
    private static void Validate(ConfigurationManager config, ServerConfigReport report)
    {
        var root = (IConfigurationRoot)config;
        report.Settings.ExplicitEndpoint = IsEndpointExplicit(config);

        // Unknown keys inside known sections, from any source.
        foreach (var section in ServerSchema.Sections)
        {
            foreach (var child in config.GetSection(section).GetChildren())
            {
                if (ServerSchema.Find(section, child.Key) is null)
                {
                    report.Errors.Add(
                        $"[{section}] {child.Key}: unknown setting (from {SourceOf(root, child.Path)})" +
                        Hint(child.Key, ServerSchema.KeysOf(section)));
                }
            }
        }

        foreach (var setting in ServerSchema.Settings)
        {
            var configured = config[setting.ConfigKey];
            var raw = (configured ?? setting.Default ?? string.Empty).Trim();
            var source = configured is null ? "default" : SourceOf(root, setting.ConfigKey);

            void Fail(string message) => report.Errors.Add($"[{setting.Section}] {setting.Key}: {message}");

            // A setting without a default (paths.ai) is simply "not set" when absent.
            var unset = configured is null && setting.Default is null;

            var resolved = setting.Section switch
            {
                _ when unset => null,
                "server" => ValidateServer(setting, raw, report.Settings, Fail),
                "tls" => ValidateTls(setting, raw, report, Fail),
                "paths" => ValidatePath(setting, raw, report, Fail),
                "proxy" => ValidateProxy(setting, raw, report.Settings, Fail),
                _ => ValidateLog(setting, raw, report.Settings, Fail),
            };

            report.Values.Add(new SettingView(
                setting.Section,
                setting.Key,
                unset ? "(not set)" : Display(setting, raw),
                source,
                resolved,
                IsAppliedNow(setting, report.Settings)));
        }

        CheckCombinations(report);
        CheckTls(report);
        CheckCompetingEndpoints(config, report);
    }

    /// <summary>
    /// Whether the setting really takes effect now: the endpoint and certificate settings only when [server]
    /// configures the endpoint explicitly (and the certificate only for https).
    /// </summary>
    private static bool IsAppliedNow(ServerSchema.Setting setting, ServerSettings settings)
    {
        if (!ServerSchema.IsApplied(setting.Section, setting.Key))
        {
            return false;
        }

        return setting.Section switch
        {
            "server" => settings.ExplicitEndpoint,
            "tls" => settings.ExplicitEndpoint && settings.Protocol == "https",
            _ => true,
        };
    }
}
