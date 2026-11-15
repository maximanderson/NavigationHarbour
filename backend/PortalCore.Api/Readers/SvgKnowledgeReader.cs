using System.Xml.Linq;
using PortalCore.Api.Models;
using PortalCore.Api.Readers.Base;

namespace PortalCore.Api.Readers;

public class SvgKnowledgeReader : FileBasedReaderBase
{
    public SvgKnowledgeReader(ILogger<SvgKnowledgeReader> logger) : base(logger)
    {
    }

    public override string SourceType => "svg";

    public override string SupportedFolder => "svg";

    public override List<KnowledgeChunk> ReadFromFile(string filePath)
    {
        var chunks = new List<KnowledgeChunk>();

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("SVG file not found: {FilePath}", filePath);
            return chunks;
        }

        _logger.LogInformation("Reading SVG knowledge from: {FilePath}", filePath);

        try
        {
            var doc = XDocument.Load(filePath);
            var dashboardName = Path.GetFileNameWithoutExtension(filePath);

            // Extract TagUses blocks
            var tagUsesChunks = ExtractTagUsesChunks(doc, dashboardName);
            chunks.AddRange(tagUsesChunks);

            // Extract Symbol chunks
            var symbolChunks = ExtractSymbolChunks(doc, dashboardName);
            chunks.AddRange(symbolChunks);

            // Create dashboard summary
            var summaryChunk = CreateDashboardSummary(doc, dashboardName, chunks);
            chunks.Add(summaryChunk);

            _logger.LogInformation("Successfully read {Count} knowledge chunks from SVG: {Dashboard}", 
                chunks.Count, dashboardName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading SVG file: {FilePath}", filePath);
        }

        return chunks;
    }

    private List<KnowledgeChunk> ExtractTagUsesChunks(XDocument doc, string dashboardName)
    {
        var chunks = new List<KnowledgeChunk>();
        var chunkIndex = 0;

        // Find all TagUses elements (match by local name to handle namespace variations)
        var tagUsesElements = doc.Descendants()
            .Where(e => e.Name.LocalName == "TagUses")
            .ToList();

        _logger.LogInformation("Found {Count} TagUses blocks in {Dashboard}", 
            tagUsesElements.Count, dashboardName);

        foreach (var tagUses in tagUsesElements)
        {
            var rawTag = tagUses.Attribute("tag")?.Value;
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                continue;
            }

            // Normalize the PI tag
            var (piServer, piTag) = NormalizeTagName(rawTag);

            // Get associated symbols
            var symbols = tagUses.Descendants()
                .Where(e => e.Name.LocalName == "Symbol")
                .Select(s => new
                {
                    Name = s.Attribute("name")?.Value ?? "Unknown",
                    TypeCode = s.Attributes()
                        .FirstOrDefault(a => a.Name.LocalName == "Type")?.Value ?? "unknown"
                })
                .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                .ToList();

            var symbolNames = symbols.Select(s => s.Name).ToList();
            var symbolTypeCodes = symbols.Select(s => s.TypeCode).ToList();

            // Create content
            var symbolsText = symbolNames.Count > 0
                ? $"symbols {string.Join(" and ", symbolNames)}"
                : "symbols";

            var content = $"PI tag {piTag} from PI server {piServer} is used in SVG dashboard {dashboardName} by {symbolsText}.";

            // Create chunk
            var chunk = new KnowledgeChunk
            {
                Id = $"svg:{dashboardName}:tag:{chunkIndex}",
                SourceType = "svg",
                SourceFile = $"{dashboardName}.svg",
                Title = $"PI Tag Usage: {piTag}",
                Content = content,
                ChunkIndex = chunkIndex,
                Metadata = new Dictionary<string, string>
                {
                    { "ObjectType", "pi_tag_usage" },
                    { "RawTag", rawTag },
                    { "PiServer", piServer },
                    { "PiTag", piTag },
                    { "SymbolNames", string.Join(";", symbolNames) },
                    { "SymbolTypeCodes", string.Join(";", symbolTypeCodes) },
                    { "DashboardName", dashboardName }
                }
            };

            chunks.Add(chunk);
            chunkIndex++;
        }

        return chunks;
    }

    private List<KnowledgeChunk> ExtractSymbolChunks(XDocument doc, string dashboardName)
    {
        var chunks = new List<KnowledgeChunk>();
        var chunkIndex = 0;

        // Find all TagUses elements to associate symbols with tags
        var tagUsesElements = doc.Descendants()
            .Where(e => e.Name.LocalName == "TagUses")
            .ToList();

        foreach (var tagUses in tagUsesElements)
        {
            var rawTag = tagUses.Attribute("tag")?.Value;
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                continue;
            }

            var (piServer, piTag) = NormalizeTagName(rawTag);

            // Get symbols for this tag
            var symbols = tagUses.Descendants()
                .Where(e => e.Name.LocalName == "Symbol")
                .ToList();

            foreach (var symbol in symbols)
            {
                var symbolName = symbol.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(symbolName))
                {
                    continue;
                }

                var typeCode = symbol.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "Type")?.Value ?? "unknown";

                var symbolType = MapSymbolType(typeCode);

                var content = $"SVG symbol {symbolName} uses PI tag {piTag}. Symbol type code is {typeCode}, interpreted as {symbolType}.";

                var chunk = new KnowledgeChunk
                {
                    Id = $"svg:{dashboardName}:symbol:{chunkIndex}",
                    SourceType = "svg",
                    SourceFile = $"{dashboardName}.svg",
                    Title = $"Symbol: {symbolName}",
                    Content = content,
                    ChunkIndex = chunkIndex,
                    Metadata = new Dictionary<string, string>
                    {
                        { "ObjectType", "symbol" },
                        { "SymbolName", symbolName },
                        { "SymbolTypeCode", typeCode },
                        { "SymbolType", symbolType },
                        { "PiTag", piTag },
                        { "PiServer", piServer },
                        { "RawTag", rawTag },
                        { "DashboardName", dashboardName }
                    }
                };

                chunks.Add(chunk);
                chunkIndex++;
            }
        }

        return chunks;
    }

    private KnowledgeChunk CreateDashboardSummary(XDocument doc, string dashboardName, List<KnowledgeChunk> existingChunks)
    {
        // Count tags and symbols from extracted chunks
        var piTagCount = existingChunks.Count(c => c.Metadata.GetValueOrDefault("ObjectType") == "pi_tag_usage");
        var symbolCount = existingChunks.Count(c => c.Metadata.GetValueOrDefault("ObjectType") == "symbol");

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

        var content = $"SVG dashboard {dashboardName} contains {piTagCount} PI tag usages and {symbolCount} linked SVG symbols. Referenced PI servers: {serversText}.";

        var chunk = new KnowledgeChunk
        {
            Id = $"svg:{dashboardName}:summary",
            SourceType = "svg",
            SourceFile = $"{dashboardName}.svg",
            Title = $"Dashboard Summary: {dashboardName}",
            Content = content,
            ChunkIndex = 0,
            Metadata = new Dictionary<string, string>
            {
                { "ObjectType", "dashboard_summary" },
                { "DashboardName", dashboardName },
                { "PiTagCount", piTagCount.ToString() },
                { "SymbolCount", symbolCount.ToString() },
                { "PiServers", serversText }
            }
        };

        return chunk;
    }

    private (string piServer, string piTag) NormalizeTagName(string rawTag)
    {
        if (string.IsNullOrWhiteSpace(rawTag))
        {
            return ("unknown", rawTag ?? "");
        }

        // Handle format like \\server\tag.name
        if (rawTag.StartsWith("\\\\"))
        {
            var parts = rawTag.TrimStart('\\').Split('\\', 2);
            if (parts.Length == 2)
            {
                return (parts[0], parts[1]);
            }
        }

        // If no server found, return unknown server
        return ("unknown", rawTag);
    }

    private string MapSymbolType(string typeCode)
    {
        return typeCode switch
        {
            "4" => "label_or_status_symbol",
            "7" => "numeric_or_value_display",
            "12" => "bar_or_level_indicator",
            _ => "unknown"
        };
    }
}
