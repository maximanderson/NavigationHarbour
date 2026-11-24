using Portal.Api.Models;

namespace Portal.Api.Abstractions;

/// <summary>
/// Interface for components that read knowledge chunks from specific file types
/// </summary>
public interface IKnowledgeReader
{
    /// <summary>
    /// Gets the source type identifier (e.g., "csv", "pdf", "json")
    /// </summary>
    string SourceType { get; }

    /// <summary>
    /// Gets the folder name where this reader's files are stored
    /// </summary>
    string SupportedFolder { get; }

    /// <summary>
    /// Reads knowledge chunks from the specified file
    /// </summary>
    /// <param name="filePath">Path to the file to read</param>
    /// <returns>List of knowledge chunks extracted from the file</returns>
    List<KnowledgeChunk> ReadFromFile(string filePath);
}
