using System.Text.Json;
using PortalCore.Api.Models;
using PortalCore.Api.Readers.Base;

namespace PortalCore.Api.Readers;

public class PiVisionJsonKnowledgeReader : FileBasedReaderBase
{
    public PiVisionJsonKnowledgeReader(ILogger<PiVisionJsonKnowledgeReader> logger) : base(logger)
    {
    }

    public override string SourceType => "pivision";

    public override string SupportedFolder => "json";

    public override List<KnowledgeChunk> ReadFromFile(string filePath)
    {
        var chunks = new List<KnowledgeChunk>();

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("PI Vision JSON file not found: {FilePath}", filePath);
            return chunks;
        }

        _logger.LogInformation("Reading PI Vision knowledge from: {FilePath}", filePath);

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var doc = JsonDocument.Parse(jsonContent);
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // Extract display root
            var display = doc.RootElement.GetProperty("Display");
            var displayName = display.TryGetProperty("Name", out var nameElement) 
                ? nameElement.GetString() ?? fileName 
                : fileName;

            // Extract symbols
            if (display.TryGetProperty("Symbols", out var symbolsElement) && 
                symbolsElement.ValueKind == JsonValueKind.Array)
            {
                var symbols = symbolsElement.EnumerateArray().ToList();

                // Extract data symbol chunks
                var dataSymbolChunks = ExtractDataSymbolChunks(symbols, displayName, fileName);
                chunks.AddRange(dataSymbolChunks);

                // Extract multistate symbol chunks
                var multistateChunks = ExtractMultistateSymbolChunks(symbols, displayName, fileName);
                chunks.AddRange(multistateChunks);

                // Extract static label chunks
                var labelChunks = ExtractStaticLabelChunks(symbols, displayName, fileName);
                chunks.AddRange(labelChunks);

                // Extract calculation source chunks
                var calcChunks = ExtractCalculationSourceChunks(symbols, displayName, fileName);
                chunks.AddRange(calcChunks);
            }

            // Create display summary chunk
            var summaryChunk = CreateDisplaySummary(display, displayName, fileName, chunks);
            chunks.Add(summaryChunk);

            _logger.LogInformation("Successfully read {Count} knowledge chunks from PI Vision display: {DisplayName}", 
                chunks.Count, displayName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading PI Vision JSON file: {FilePath}", filePath);
        }

        return chunks;
    }

    private List<KnowledgeChunk> ExtractDataSymbolChunks(
        List<JsonElement> symbols,
        string displayName,
        string fileName)
    {
        var chunks = new List<KnowledgeChunk>();
        var chunkIndex = 0;

        foreach (var symbol in symbols)
        {
            // Check for DataSources
            if (!symbol.TryGetProperty("DataSources", out var dataSourcesElement) ||
                dataSourcesElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var symbolName = symbol.TryGetProperty("Name", out var nameElement)
                ? nameElement.GetString() ?? "Unknown"
                : "Unknown";

            var symbolType = symbol.TryGetProperty("SymbolType", out var typeElement)
                ? typeElement.GetString() ?? "unknown"
                : "unknown";

            // Process each data source
            foreach (var dataSource in dataSourcesElement.EnumerateArray())
            {
                var rawDataSource = dataSource.GetString();
                if (string.IsNullOrWhiteSpace(rawDataSource))
                {
                    continue;
                }

                var (piServer, piTag) = ParsePiDataSource(rawDataSource);

                // Get static text if available
                var staticText = "";
                if (symbol.TryGetProperty("Configuration", out var config) &&
                    config.TryGetProperty("StaticText", out var staticTextElement))
                {
                    staticText = staticTextElement.GetString() ?? "";
                }

                // Build content
                var content = $"PI Vision {symbolType} symbol {symbolName} displays PI tag {piTag} from PI server {piServer} on display {displayName}.";
                if (!string.IsNullOrWhiteSpace(staticText))
                {
                    content = $"PI Vision {symbolType} symbol {symbolName} labeled \"{staticText}\" displays PI tag {piTag} from PI server {piServer} on display {displayName}.";
                }

                var chunk = new KnowledgeChunk
                {
                    Id = $"pivision:{displayName}:data:{chunkIndex}",
                    SourceType = "pivision",
                    SourceFile = $"{fileName}.json",
                    Title = $"Data Symbol: {symbolName}",
                    Content = content,
                    ChunkIndex = chunkIndex,
                    Metadata = new Dictionary<string, string>
                    {
                        { "ObjectType", "pivision_data_symbol" },
                        { "DisplayName", displayName },
                        { "SymbolName", symbolName },
                        { "SymbolType", symbolType },
                        { "RawDataSource", rawDataSource },
                        { "PiServer", piServer },
                        { "PiTag", piTag }
                    }
                };

                if (!string.IsNullOrWhiteSpace(staticText))
                {
                    chunk.Metadata["StaticText"] = staticText;
                }

                chunks.Add(chunk);
                chunkIndex++;
            }
        }

        return chunks;
    }

    private List<KnowledgeChunk> ExtractMultistateSymbolChunks(
        List<JsonElement> symbols,
        string displayName,
        string fileName)
    {
        var chunks = new List<KnowledgeChunk>();
        var chunkIndex = 0;

        foreach (var symbol in symbols)
        {
            // Check for MSDataSources or Multistates configuration
            var hasMSDataSources = symbol.TryGetProperty("MSDataSources", out var msDataSourcesElement) &&
                                   msDataSourcesElement.ValueKind == JsonValueKind.Array;
            
            var hasMultistates = false;
            if (symbol.TryGetProperty("Configuration", out var config))
            {
                hasMultistates = config.TryGetProperty("Multistates", out var multistatesElement);
            }

            if (!hasMSDataSources && !hasMultistates)
            {
                continue;
            }

            var symbolName = symbol.TryGetProperty("Name", out var nameElement)
                ? nameElement.GetString() ?? "Unknown"
                : "Unknown";

            var symbolType = symbol.TryGetProperty("SymbolType", out var typeElement)
                ? typeElement.GetString() ?? "unknown"
                : "unknown";

            // Process MS data sources if available
            if (hasMSDataSources)
            {
                foreach (var msDataSource in msDataSourcesElement.EnumerateArray())
                {
                    var rawDataSource = msDataSource.GetString();
                    if (string.IsNullOrWhiteSpace(rawDataSource))
                    {
                        continue;
                    }

                    var (piServer, piTag) = ParsePiDataSource(rawDataSource);

                    var content = $"PI Vision multistate symbol {symbolName} monitors PI tag {piTag} from PI server {piServer} and changes visual state based on configured multistate rules on display {displayName}.";

                    var chunk = new KnowledgeChunk
                    {
                        Id = $"pivision:{displayName}:multistate:{chunkIndex}",
                        SourceType = "pivision",
                        SourceFile = $"{fileName}.json",
                        Title = $"Multistate Symbol: {symbolName}",
                        Content = content,
                        ChunkIndex = chunkIndex,
                        Metadata = new Dictionary<string, string>
                        {
                            { "ObjectType", "pivision_multistate_symbol" },
                            { "DisplayName", displayName },
                            { "SymbolName", symbolName },
                            { "SymbolType", symbolType },
                            { "RawDataSource", rawDataSource },
                            { "PiServer", piServer },
                            { "PiTag", piTag },
                            { "HasMultistates", "true" }
                        }
                    };

                    chunks.Add(chunk);
                    chunkIndex++;
                }
            }
            else if (hasMultistates)
            {
                // Symbol has multistate configuration but no explicit MSDataSources
                var content = $"PI Vision multistate symbol {symbolName} has multistate configuration on display {displayName}.";

                var chunk = new KnowledgeChunk
                {
                    Id = $"pivision:{displayName}:multistate:{chunkIndex}",
                    SourceType = "pivision",
                    SourceFile = $"{fileName}.json",
                    Title = $"Multistate Symbol: {symbolName}",
                    Content = content,
                    ChunkIndex = chunkIndex,
                    Metadata = new Dictionary<string, string>
                    {
                        { "ObjectType", "pivision_multistate_symbol" },
                        { "DisplayName", displayName },
                        { "SymbolName", symbolName },
                        { "SymbolType", symbolType },
                        { "HasMultistates", "true" }
                    }
                };

                chunks.Add(chunk);
                chunkIndex++;
            }
        }

        return chunks;
    }

    private List<KnowledgeChunk> ExtractStaticLabelChunks(
        List<JsonElement> symbols,
        string displayName,
        string fileName)
    {
        var chunks = new List<KnowledgeChunk>();
        var chunkIndex = 0;

        foreach (var symbol in symbols)
        {
            var symbolName = symbol.TryGetProperty("Name", out var nameElement)
                ? nameElement.GetString() ?? "Unknown"
                : "Unknown";

            var symbolType = symbol.TryGetProperty("SymbolType", out var typeElement)
                ? typeElement.GetString() ?? "unknown"
                : "unknown";

            // Check for static text in Configuration
            if (!symbol.TryGetProperty("Configuration", out var config) ||
                !config.TryGetProperty("StaticText", out var staticTextElement))
            {
                continue;
            }

            var staticText = staticTextElement.GetString();
            if (string.IsNullOrWhiteSpace(staticText))
            {
                continue;
            }

            // Skip if symbol already has DataSources or MSDataSources (already in other chunks)
            if (symbol.TryGetProperty("DataSources", out _) ||
                symbol.TryGetProperty("MSDataSources", out _))
            {
                continue;
            }

            var content = $"PI Vision display {displayName} contains label: {staticText}.";

            var chunk = new KnowledgeChunk
            {
                Id = $"pivision:{displayName}:label:{chunkIndex}",
                SourceType = "pivision",
                SourceFile = $"{fileName}.json",
                Title = $"Static Label: {symbolName}",
                Content = content,
                ChunkIndex = chunkIndex,
                Metadata = new Dictionary<string, string>
                {
                    { "ObjectType", "pivision_static_label" },
                    { "DisplayName", displayName },
                    { "SymbolName", symbolName },
                    { "SymbolType", symbolType },
                    { "StaticText", staticText }
                }
            };

            chunks.Add(chunk);
            chunkIndex++;
        }

        return chunks;
    }

    private List<KnowledgeChunk> ExtractCalculationSourceChunks(
        List<JsonElement> symbols,
        string displayName,
        string fileName)
    {
        var chunks = new List<KnowledgeChunk>();
        var chunkIndex = 0;

        foreach (var symbol in symbols)
        {
            var symbolName = symbol.TryGetProperty("Name", out var nameElement)
                ? nameElement.GetString() ?? "Unknown"
                : "Unknown";

            var symbolType = symbol.TryGetProperty("SymbolType", out var typeElement)
                ? typeElement.GetString() ?? "unknown"
                : "unknown";

            // Check for DataSources that start with "calc:"
            if (symbol.TryGetProperty("DataSources", out var dataSourcesElement) &&
                dataSourcesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var dataSource in dataSourcesElement.EnumerateArray())
                {
                    var rawDataSource = dataSource.GetString();
                    if (string.IsNullOrWhiteSpace(rawDataSource) || !rawDataSource.StartsWith("calc:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Extract calculation source
                    var calcSource = rawDataSource.Substring(5); // Remove "calc:" prefix

                    var content = $"PI Vision symbol {symbolName} uses calculation source {calcSource} on display {displayName}.";

                    var chunk = new KnowledgeChunk
                    {
                        Id = $"pivision:{displayName}:calc:{chunkIndex}",
                        SourceType = "pivision",
                        SourceFile = $"{fileName}.json",
                        Title = $"Calculation Source: {symbolName}",
                        Content = content,
                        ChunkIndex = chunkIndex,
                        Metadata = new Dictionary<string, string>
                        {
                            { "ObjectType", "pivision_calculation_source" },
                            { "DisplayName", displayName },
                            { "SymbolName", symbolName },
                            { "SymbolType", symbolType },
                            { "CalculationSource", calcSource },
                            { "RawDataSource", rawDataSource }
                        }
                    };

                    chunks.Add(chunk);
                    chunkIndex++;
                }
            }
        }

        return chunks;
    }

    private KnowledgeChunk CreateDisplaySummary(
        JsonElement display,
        string displayName,
        string fileName,
        List<KnowledgeChunk> existingChunks)
    {
        // Count symbols
        var totalSymbolCount = 0;
        if (display.TryGetProperty("Symbols", out var symbolsElement) &&
            symbolsElement.ValueKind == JsonValueKind.Array)
        {
            totalSymbolCount = symbolsElement.GetArrayLength();
        }

        // Count data-linked symbols
        var dataLinkedCount = existingChunks.Count(c => 
            c.Metadata.GetValueOrDefault("ObjectType") == "pivision_data_symbol" ||
            c.Metadata.GetValueOrDefault("ObjectType") == "pivision_multistate_symbol");

        // Get unique PI servers
        var piServers = existingChunks
            .Where(c => c.Metadata.ContainsKey("PiServer"))
            .Select(c => c.Metadata["PiServer"])
            .Where(s => !string.IsNullOrWhiteSpace(s) && s != "unknown")
            .Distinct()
            .ToList();

        var serversText = piServers.Count > 0
            ? string.Join(", ", piServers)
            : "unknown";

        var content = $"PI Vision display {displayName} contains {totalSymbolCount} symbols, {dataLinkedCount} data-linked symbols, and references PI servers {serversText}.";

        var chunk = new KnowledgeChunk
        {
            Id = $"pivision:{displayName}:summary",
            SourceType = "pivision",
            SourceFile = $"{fileName}.json",
            Title = $"Display Summary: {displayName}",
            Content = content,
            ChunkIndex = 0,
            Metadata = new Dictionary<string, string>
            {
                { "ObjectType", "pivision_display_summary" },
                { "DisplayName", displayName },
                { "SymbolCount", totalSymbolCount.ToString() },
                { "DataLinkedSymbolCount", dataLinkedCount.ToString() },
                { "PiServers", serversText }
            }
        };

        // Add DisplayId if available
        if (display.TryGetProperty("Id", out var idElement))
        {
            var displayId = idElement.GetString();
            if (!string.IsNullOrWhiteSpace(displayId))
            {
                chunk.Metadata["DisplayId"] = displayId;
            }
        }

        return chunk;
    }

    private (string piServer, string piTag) ParsePiDataSource(string rawDataSource)
    {
        if (string.IsNullOrWhiteSpace(rawDataSource))
        {
            return ("unknown", rawDataSource ?? "");
        }

        try
        {
            // Handle format like: pi:\\ods?a29d3dd8-16ac-4f0b-a48e-a8fbf37fa40e\adedp-0301-cit1502-cndpva?268642
            if (rawDataSource.StartsWith("pi:", StringComparison.OrdinalIgnoreCase))
            {
                // Extract server name (between pi:\\ and ?)
                var afterPrefix = rawDataSource.Substring(3).TrimStart('\\');
                var serverEnd = afterPrefix.IndexOf('?');
                
                if (serverEnd > 0)
                {
                    var server = afterPrefix.Substring(0, serverEnd);
                    
                    // Extract tag name (between \ and ? or end)
                    var afterServer = afterPrefix.Substring(serverEnd + 1);
                    var tagStart = afterServer.LastIndexOf('\\');
                    
                    if (tagStart >= 0)
                    {
                        var afterBackslash = afterServer.Substring(tagStart + 1);
                        var tagEnd = afterBackslash.IndexOf('?');
                        var tag = tagEnd > 0 
                            ? afterBackslash.Substring(0, tagEnd) 
                            : afterBackslash;
                        
                        return (server, tag);
                    }
                }
            }
        }
        catch
        {
            // Parsing failed, fall through to default behavior
        }

        // If parsing fails, return the original value as tag
        return ("unknown", rawDataSource);
    }
}
