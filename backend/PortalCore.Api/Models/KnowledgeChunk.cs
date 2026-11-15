namespace PortalCore.Api.Models;

public class KnowledgeChunk
{
    public string Id { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
