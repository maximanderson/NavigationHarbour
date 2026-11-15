using PortalCore.Api.Abstractions;
using PortalCore.Api.Models;

namespace PortalCore.Api.Readers.Base;

/// <summary>
/// Base class for file-based knowledge readers
/// Provides common logger support for all file readers
/// </summary>
public abstract class FileBasedReaderBase : IKnowledgeReader
{
    protected readonly ILogger _logger;

    protected FileBasedReaderBase(ILogger logger)
    {
        _logger = logger;
    }

    public abstract string SourceType { get; }

    public abstract string SupportedFolder { get; }

    public abstract List<KnowledgeChunk> ReadFromFile(string filePath);
}
