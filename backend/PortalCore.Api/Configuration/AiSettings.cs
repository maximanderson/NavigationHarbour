namespace PortalCore.Api.Configuration;

public class AiSettings
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "Ollama";
    public string KnowledgeFolder { get; set; } = "Data/knowledge";
    public string PromptFolder { get; set; } = "Data/prompts";
    public KnowledgeFilesSettings KnowledgeFiles { get; set; } = new KnowledgeFilesSettings();
    public ProviderSettings Providers { get; set; } = new ProviderSettings();
    public VectorDbSettings VectorDb { get; set; } = new VectorDbSettings();
}

public class ProviderSettings
{
    public LlmProviderSettings Ollama { get; set; } = new LlmProviderSettings();
    public LlmProviderSettings OpenAI { get; set; } = new LlmProviderSettings();
}

public class LlmProviderSettings
{
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
}

public class KnowledgeFilesSettings
{
    public bool Enabled { get; set; } = true;
}

public class VectorDbSettings
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "Qdrant";
    public string Endpoint { get; set; } = "http://localhost:6333";
    public string Collection { get; set; } = "portalcore-knowledge";
    // Future: embedding model, authentication, etc.
}
