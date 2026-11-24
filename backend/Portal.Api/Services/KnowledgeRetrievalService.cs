using System.Diagnostics;
using Portal.Api.Abstractions;
using Portal.Api.Models;

namespace Portal.Api.Services;

/// <summary>
/// Central orchestration layer for knowledge retrieval.
/// Coordinates multiple knowledge sources, performs filtering, merging, and ranking.
/// Future-ready for hybrid retrieval (file sources + vector DB sources).
/// </summary>
public class KnowledgeRetrievalService
{
    private readonly IEnumerable<IKnowledgeSource> _knowledgeSources;
    private readonly ILogger<KnowledgeRetrievalService> _logger;

    public KnowledgeRetrievalService(
        IEnumerable<IKnowledgeSource> knowledgeSources,
        ILogger<KnowledgeRetrievalService> logger)
    {
        _knowledgeSources = knowledgeSources;
        _logger = logger;
    }

    /// <summary>
    /// Orchestrates knowledge retrieval across all enabled sources.
    /// Calls all IKnowledgeSource implementations, merges results, and performs keyword filtering.
    /// </summary>
    public async Task<List<KnowledgeChunk>> SearchAsync(string query)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("Empty query received");
            return new List<KnowledgeChunk>();
        }

        _logger.LogInformation("Knowledge retrieval orchestration started. QueryLength={QueryLength}", query.Length);

        try
        {
            // Step 1: Retrieve chunks from all enabled knowledge sources
            var allChunks = await RetrieveFromAllSourcesAsync(query);

            _logger.LogInformation(
                "Retrieved chunks from all sources. TotalChunks={TotalChunks}",
                allChunks.Count);

            // Step 2: Extract keywords from query for filtering
            var keywords = ExtractKeywords(query);

            _logger.LogInformation(
                "Extracted keywords for filtering. KeywordCount={KeywordCount}",
                keywords.Count);

            // Step 3: Perform keyword-based filtering on Title, Content, and Metadata
            var filteredChunks = FilterChunksByKeywords(allChunks, keywords);

            stopwatch.Stop();

            _logger.LogInformation(
                "Knowledge retrieval completed. QueryLength={QueryLength}, KeywordCount={KeywordCount}, TotalChunks={TotalChunks}, FilteredChunks={FilteredChunks}, ElapsedMs={ElapsedMs}",
                query.Length,
                keywords.Count,
                allChunks.Count,
                filteredChunks.Count,
                stopwatch.ElapsedMilliseconds);

            return filteredChunks;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Knowledge retrieval failed. QueryLength={QueryLength}, ElapsedMs={ElapsedMs}",
                query.Length,
                stopwatch.ElapsedMilliseconds);
            
            return new List<KnowledgeChunk>();
        }
    }

    /// <summary>
    /// Retrieves chunks from all enabled IKnowledgeSource implementations.
    /// Future: Can add ranking, deduplication, and merging strategies here.
    /// </summary>
    private async Task<List<KnowledgeChunk>> RetrieveFromAllSourcesAsync(string query)
    {
        var allChunks = new List<KnowledgeChunk>();

        foreach (var source in _knowledgeSources)
        {
            try
            {
                var chunks = await source.SearchAsync(query);
                allChunks.AddRange(chunks);
                
                _logger.LogInformation(
                    "Retrieved chunks from source. SourceType={SourceType}, ChunkCount={ChunkCount}",
                    source.GetType().Name,
                    chunks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to retrieve from source. SourceType={SourceType}",
                    source.GetType().Name);
            }
        }

        return allChunks;
    }

    /// <summary>
    /// Filters chunks by matching keywords in Title, Content, or Metadata values.
    /// </summary>
    private List<KnowledgeChunk> FilterChunksByKeywords(List<KnowledgeChunk> chunks, List<string> keywords)
    {
        if (keywords.Count == 0)
        {
            // No meaningful keywords extracted, return all chunks
            return chunks;
        }

        var filteredChunks = chunks
            .Where(chunk =>
            {
                var titleLower = chunk.Title.ToLowerInvariant();
                var contentLower = chunk.Content.ToLowerInvariant();
                
                // Check metadata values
                var metadataValues = chunk.Metadata.Values
                    .Select(v => v.ToLowerInvariant());
                
                // Match if ANY keyword is found in Title, Content, or Metadata values
                return keywords.Any(keyword =>
                    titleLower.Contains(keyword) ||
                    contentLower.Contains(keyword) ||
                    metadataValues.Any(metaValue => metaValue.Contains(keyword)));
            })
            .ToList();

        return filteredChunks;
    }

    /// <summary>
    /// Extracts meaningful keywords from the query for filtering.
    /// Filters out weak tokens (length <= 3) and removes punctuation.
    /// </summary>
    private List<string> ExtractKeywords(string query)
    {
        // Convert to lowercase
        var queryLower = query.ToLowerInvariant();

        // Remove punctuation and split into tokens
        var tokens = new List<string>();
        var currentToken = new System.Text.StringBuilder();

        foreach (var ch in queryLower)
        {
            if (char.IsLetterOrDigit(ch))
            {
                currentToken.Append(ch);
            }
            else if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
                currentToken.Clear();
            }
        }

        // Add last token if exists
        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        // Filter out weak tokens (length <= 3)
        var keywords = tokens
            .Where(token => token.Length > 3)
            .Distinct()
            .ToList();

        return keywords;
    }
}
