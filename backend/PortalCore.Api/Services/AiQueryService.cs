using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PortalCore.Api.Abstractions;
using PortalCore.Api.Configuration;
using PortalCore.Api.Models;

namespace PortalCore.Api.Services;

public class AiQueryService
{
    private readonly IWebHostEnvironment _environment;
    private readonly HttpClient _httpClient;
    private readonly AiSettings _aiSettings;
    private readonly ILogger<AiQueryService> _logger;
    private readonly KnowledgeRetrievalService _knowledgeRetrievalService;
    private readonly IPromptTemplateProvider _promptTemplateProvider;

    public AiQueryService(
        IWebHostEnvironment environment, 
        IHttpClientFactory httpClientFactory, 
        IOptions<AiSettings> aiSettings, 
        ILogger<AiQueryService> logger,
        KnowledgeRetrievalService knowledgeRetrievalService,
        IPromptTemplateProvider promptTemplateProvider)
    {
        _environment = environment;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(300); // 5 minute timeout for LLM requests
        _aiSettings = aiSettings.Value;
        _logger = logger;
        _knowledgeRetrievalService = knowledgeRetrievalService;
        _promptTemplateProvider = promptTemplateProvider;
    }

    public async Task<string> QueryAsync(string prompt)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));
        }

        _logger.LogInformation("AI query received. PromptLength={PromptLength}", prompt.Length);

        // Check if AI is enabled
        if (!_aiSettings.Enabled)
        {
            _logger.LogInformation("AI assistant is disabled via configuration.");
            return "AI assistant is currently disabled.";
        }

        try
        {
            // Retrieval-assisted context assembly using KnowledgeRetrievalService orchestration
            var retrievedChunks = await _knowledgeRetrievalService.SearchAsync(prompt);
            
            _logger.LogInformation(
                "Retrieval-assisted context assembly. RetrievalMatchCount={MatchCount}",
                retrievedChunks.Count);

            // Group chunks by SourceType for prompt-template architecture
            var groupedChunks = retrievedChunks
                .GroupBy(chunk => chunk.SourceType)
                .ToList();

            _logger.LogInformation(
                "Chunks grouped by SourceType. GroupCount={GroupCount}",
                groupedChunks.Count);

            // Assemble final prompt using SourceType-specific templates
            var ragPrompt = await AssemblePromptAsync(groupedChunks, prompt);
            
            // Prompt content is intentionally never logged (privacy). Only its size is.
            _logger.LogDebug("LLM prompt assembled. PromptCharCount={PromptCharCount}", ragPrompt.Length);

            // Route to appropriate provider based on configuration
            string answer;
            switch (_aiSettings.Provider.ToLowerInvariant())
            {
                case "ollama":
                    answer = await QueryOllamaAsync(ragPrompt, prompt.Length, stopwatch, retrievedChunks.Count > 0);
                    break;
                
                case "openai":
                    answer = await QueryOpenAiAsync(ragPrompt, prompt.Length, stopwatch, retrievedChunks.Count > 0);
                    break;
                
                default:
                    throw new InvalidOperationException($"Unsupported AI provider: {_aiSettings.Provider}. Supported providers: Ollama, OpenAI");
            }

            return answer;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "AI query failed with exception. PromptLength={PromptLength}, ElapsedMs={ElapsedMs}, Success={Success}",
                prompt.Length,
                stopwatch.ElapsedMilliseconds,
                false);
            return "AI assistant is currently unavailable.";
        }
    }

    private async Task<string> AssemblePromptAsync(
        List<IGrouping<string, KnowledgeChunk>> groupedChunks, 
        string userQuestion)
    {
        if (groupedChunks.Count == 0)
        {
            // No chunks retrieved, use default prompt with empty context
            var defaultPrompt = await _promptTemplateProvider.GetPromptAsync("csv");
            return defaultPrompt
                .Replace("{context}", "No matching knowledge chunks found.")
                .Replace("{question}", userQuestion);
        }

        // For now, we use the first SourceType's prompt template
        // Future enhancement: merge multiple SourceType prompts/contexts
        var firstGroup = groupedChunks[0];
        var sourceType = firstGroup.Key;
        var chunks = firstGroup.ToList();

        _logger.LogInformation(
            "Assembling prompt for SourceType. SourceType={SourceType}, ChunkCount={ChunkCount}",
            sourceType,
            chunks.Count);

        // Load prompt template for this SourceType
        var promptTemplate = await _promptTemplateProvider.GetPromptAsync(sourceType);

        // Build context block from chunks
        var contextBlock = BuildContextBlock(chunks);

        // Inject context and question into template
        var assembledPrompt = promptTemplate
            .Replace("{context}", contextBlock)
            .Replace("{question}", userQuestion);

        return assembledPrompt;
    }

    private string BuildContextBlock(List<KnowledgeChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return "No matching knowledge chunks found.";
        }

        var contextBuilder = new StringBuilder();
        
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            contextBuilder.AppendLine(chunk.Content);
            
            if (i < chunks.Count - 1)
            {
                contextBuilder.AppendLine();
            }
        }

        return contextBuilder.ToString();
    }

    private async Task<string> QueryOllamaAsync(string ragPrompt, int originalPromptLength, Stopwatch stopwatch, bool contextUsed)
    {
        _logger.LogInformation(
            "Sending request to Ollama provider. Source={Source}, PromptLength={PromptLength}, ContextUsed={ContextUsed}",
            "Ollama",
            originalPromptLength,
            contextUsed);

        var ollamaRequest = new
        {
            model = _aiSettings.Providers.Ollama.Model,
            prompt = ragPrompt,
            stream = false,
            options = new
            {
                temperature = 0
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(ollamaRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            _aiSettings.Providers.Ollama.Endpoint,
            content);

        if (!response.IsSuccessStatusCode)
        {
            stopwatch.Stop();
            _logger.LogError(
                "LLM request failed. Source={Source}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}, Success={Success}",
                "Ollama",
                response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                false);
            throw new HttpRequestException($"Ollama request failed with status code {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var answer = doc.RootElement
            .GetProperty("response")
            .GetString();

        stopwatch.Stop();

        if (string.IsNullOrWhiteSpace(answer))
        {
            _logger.LogWarning(
                "Empty AI response received. Source={Source}, ResponseLength={ResponseLength}, ElapsedMs={ElapsedMs}",
                "Ollama",
                0,
                stopwatch.ElapsedMilliseconds);
            return string.Empty;
        }

        _logger.LogInformation(
            "AI response generated successfully. Source={Source}, PromptLength={PromptLength}, ResponseLength={ResponseLength}, ElapsedMs={ElapsedMs}, Success={Success}",
            "Ollama",
            originalPromptLength,
            answer.Length,
            stopwatch.ElapsedMilliseconds,
            true);

        return answer;
    }

    private async Task<string> QueryOpenAiAsync(string ragPrompt, int originalPromptLength, Stopwatch stopwatch, bool contextUsed)
    {
        _logger.LogInformation(
            "Sending request to OpenAI provider. Source={Source}, PromptLength={PromptLength}, ContextUsed={ContextUsed}",
            "OpenAI",
            originalPromptLength,
            contextUsed);

        var openAiRequest = new
        {
            model = _aiSettings.Providers.OpenAI.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = ragPrompt
                }
            },
            temperature = 0
        };

        var content = new StringContent(
            JsonSerializer.Serialize(openAiRequest),
            Encoding.UTF8,
            "application/json");

        // Add Authorization header for OpenAI
        if (!string.IsNullOrWhiteSpace(_aiSettings.Providers.OpenAI.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiSettings.Providers.OpenAI.ApiKey}");
        }

        var response = await _httpClient.PostAsync(
            _aiSettings.Providers.OpenAI.Endpoint,
            content);

        if (!response.IsSuccessStatusCode)
        {
            stopwatch.Stop();
            // The provider error body is not logged (may echo request content).
            _logger.LogError(
                "LLM request failed. Source={Source}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}, Success={Success}",
                "OpenAI",
                response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                false);
            throw new HttpRequestException($"OpenAI request failed with status code {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        string? answer = null;
        
        // Parse OpenAI response format: choices[0].message.content
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("content", out var contentProperty))
                {
                    answer = contentProperty.GetString();
                }
            }
        }

        stopwatch.Stop();

        if (string.IsNullOrWhiteSpace(answer))
        {
            _logger.LogWarning(
                "Empty AI response received. Source={Source}, ResponseLength={ResponseLength}, ElapsedMs={ElapsedMs}",
                "OpenAI",
                0,
                stopwatch.ElapsedMilliseconds);
            return string.Empty;
        }

        _logger.LogInformation(
            "AI response generated successfully. Source={Source}, PromptLength={PromptLength}, ResponseLength={ResponseLength}, ElapsedMs={ElapsedMs}, Success={Success}",
            "OpenAI",
            originalPromptLength,
            answer.Length,
            stopwatch.ElapsedMilliseconds,
            true);

        return answer;
    }
}
