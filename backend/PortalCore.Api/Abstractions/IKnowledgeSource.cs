using PortalCore.Api.Models;

namespace PortalCore.Api.Abstractions;

/// <summary>
/// Represents a source of knowledge that can be searched
/// </summary>
public interface IKnowledgeSource
{
    /// <summary>
    /// Searches for knowledge chunks that match the given query
    /// </summary>
    /// <param name="query">The search query</param>
    /// <returns>A list of matching knowledge chunks</returns>
    Task<List<KnowledgeChunk>> SearchAsync(string query);
}
