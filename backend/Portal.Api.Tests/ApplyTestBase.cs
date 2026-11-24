using Microsoft.Extensions.Configuration;

namespace Portal.Api.Tests;

public abstract class ApplyTestBase : ServerConfigTestBase
{
    private const string ShippedAppSettings = """
        {
          "PortalLogging": {
            "Level": "info",
            "Console": true,
            "File": { "Enabled": true, "Directory": "logs", "MaxFileSizeMb": 10, "RetainDays": 14 }
          },
          "AiSettings": { "KnowledgeFolder": "Data/knowledge", "PromptFolder": "Data/prompts" }
        }
        """;

    /// <summary>
    /// Configuration as the real host builds it: the shipped appsettings.json (development defaults)
    /// is a JSON file source, which is what the server.ini sources are positioned after.
    /// </summary>
    protected ConfigurationManager WithShippedDefaults()
    {
        var directory = Path.Combine(Home, "shipped");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "appsettings.json"), ShippedAppSettings);

        var config = new ConfigurationManager();
        config.SetBasePath(directory);
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
        return config;
    }
}
