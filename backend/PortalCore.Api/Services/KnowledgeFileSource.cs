using Microsoft.Extensions.Options;
using PortalCore.Api.Abstractions;
using PortalCore.Api.Configuration;
using PortalCore.Api.Models;

namespace PortalCore.Api.Services;

/// <summary>
/// Knowledge source that reads from local files using registered readers.
/// This source simply loads and returns chunks - orchestration is handled by KnowledgeRetrievalService.
/// </summary>
public class KnowledgeFileSource : IKnowledgeSource
{
    private readonly IEnumerable<IKnowledgeReader> _readers;
    private readonly IWebHostEnvironment _environment;
    private readonly AiSettings _aiSettings;
    private readonly ILogger<KnowledgeFileSource> _logger;

    public KnowledgeFileSource(
        IEnumerable<IKnowledgeReader> readers,
        IWebHostEnvironment environment,
        IOptions<AiSettings> aiSettings,
        ILogger<KnowledgeFileSource> logger)
    {
        _readers = readers;
        _environment = environment;
        _aiSettings = aiSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns all knowledge chunks from file sources.
    /// No filtering is performed - orchestration is handled by KnowledgeRetrievalService.
    /// </summary>
    public Task<List<KnowledgeChunk>> SearchAsync(string query)
    {
        try
        {
            var allChunks = LoadAllKnowledgeChunks();
            
            _logger.LogInformation(
                "File source loaded chunks. TotalChunks={ChunkCount}",
                allChunks.Count);

            return Task.FromResult(allChunks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load knowledge chunks from files");
            return Task.FromResult(new List<KnowledgeChunk>());
        }
    }

    private List<KnowledgeChunk> LoadAllKnowledgeChunks()
    {
        var allChunks = new List<KnowledgeChunk>();

        foreach (var reader in _readers)
        {
            var folderPath = Path.Combine(_environment.ContentRootPath, _aiSettings.KnowledgeFolder, reader.SupportedFolder);
            
            if (!Directory.Exists(folderPath))
            {
                _logger.LogWarning("{SourceType} folder does not exist: {FolderPath}", reader.SourceType, folderPath);
                continue;
            }

            var filePattern = $"*.{reader.SourceType}";
            var files = Directory.GetFiles(folderPath, filePattern);
            
            if (files.Length == 0)
            {
                _logger.LogWarning("No {SourceType} files found in: {FolderPath}", reader.SourceType, folderPath);
                continue;
            }

            _logger.LogInformation("Loading {SourceType} files from: {FolderPath}, Count={FileCount}", 
                reader.SourceType, folderPath, files.Length);

            foreach (var file in files)
            {
                var chunks = reader.ReadFromFile(file);
                allChunks.AddRange(chunks);
            }

            _logger.LogInformation("Loaded {Count} knowledge chunks from {FileCount} {SourceType} files", 
                allChunks.Count, files.Length, reader.SourceType);
        }

        return allChunks;
    }
}
