using PortalCore.Api.Models;
using PortalCore.Api.Readers.Base;

namespace PortalCore.Api.Readers;

public class CsvKnowledgeReader : FileBasedReaderBase
{
    public CsvKnowledgeReader(ILogger<CsvKnowledgeReader> logger) : base(logger)
    {
    }

    public override string SourceType => "csv";

    public override string SupportedFolder => "csv";

    public override List<KnowledgeChunk> ReadFromFile(string filePath)
    {
        var chunks = new List<KnowledgeChunk>();

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("CSV file not found: {FilePath}", filePath);
            return chunks;
        }

        _logger.LogInformation("Reading CSV knowledge from: {FilePath}", filePath);

        var lines = File.ReadAllLines(filePath);
        
        if (lines.Length == 0)
        {
            _logger.LogWarning("CSV file is empty: {FilePath}", filePath);
            return chunks;
        }

        // First line is header
        var headers = ParseCsvLine(lines[0]);
        
        if (headers.Length == 0)
        {
            _logger.LogWarning("CSV header is empty: {FilePath}", filePath);
            return chunks;
        }

        // Get source file name
        var sourceFileName = Path.GetFileName(filePath);

        // Process data rows
        int chunkIndex = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Ignore empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            
            if (values.Length == 0)
            {
                continue;
            }

            // Create KnowledgeChunk
            var chunk = CreateChunk(headers, values, sourceFileName, chunkIndex);
            chunks.Add(chunk);
            chunkIndex++;
        }

        _logger.LogInformation("Successfully read {Count} knowledge chunks from CSV", chunks.Count);
        
        return chunks;
    }

    private string[] ParseCsvLine(string line)
    {
        // Simple comma-separated parser
        return line.Split(',')
                   .Select(field => field.Trim())
                   .ToArray();
    }

    private KnowledgeChunk CreateChunk(string[] headers, string[] values, string sourceFileName, int chunkIndex)
    {
        var chunk = new KnowledgeChunk
        {
            SourceType = "csv",
            SourceFile = sourceFileName,
            ChunkIndex = chunkIndex
        };

        // Determine title: use "City" column if available, otherwise use first column or row index
        string title = string.Empty;
        
        // Look for "City" column
        int cityColumnIndex = Array.FindIndex(headers, h => h.Equals("City", StringComparison.OrdinalIgnoreCase));
        if (cityColumnIndex >= 0 && cityColumnIndex < values.Length && !string.IsNullOrWhiteSpace(values[cityColumnIndex]))
        {
            title = values[cityColumnIndex];
        }
        else if (values.Length > 0)
        {
            // Fallback to first column
            title = values[0];
        }
        else
        {
            // Fallback to row index
            title = $"Row {chunkIndex}";
        }
        
        chunk.Title = title;
        chunk.Id = $"{sourceFileName}_{chunkIndex}_{title.Replace(" ", "_")}";

        // Build JSON-like structured content
        var contentLines = new List<string>();
        contentLines.Add("{");
        
        for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                var comma = i < Math.Min(headers.Length, values.Length) - 1 ? "," : "";
                contentLines.Add($"  \"{headers[i]}\": \"{values[i]}\"{comma}");
            }
        }
        
        contentLines.Add("}");
        
        chunk.Content = string.Join("\n", contentLines);

        // Populate metadata dictionary with CSV column/value pairs
        chunk.Metadata = new Dictionary<string, string>();
        for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                chunk.Metadata[headers[i]] = values[i];
            }
        }

        return chunk;
    }
}
