using Microsoft.Extensions.Options;
using PortalCore.Api.Abstractions;
using PortalCore.Api.Configuration;

namespace PortalCore.Api.Services;

public class FilePromptTemplateProvider : IPromptTemplateProvider
{
    private readonly IWebHostEnvironment _environment;
    private readonly AiSettings _aiSettings;
    private readonly ILogger<FilePromptTemplateProvider> _logger;

    public FilePromptTemplateProvider(
        IWebHostEnvironment environment,
        IOptions<AiSettings> aiSettings,
        ILogger<FilePromptTemplateProvider> logger)
    {
        _environment = environment;
        _aiSettings = aiSettings.Value;
        _logger = logger;
    }

    public async Task<string> GetPromptAsync(string sourceType)
    {
        try
        {
            // Map semantic sourceType to physical folder name
            var promptFolder = GetPromptFolderForSourceType(sourceType);
            
            var promptPath = Path.Combine(
                _environment.ContentRootPath,
                _aiSettings.PromptFolder,
                promptFolder,
                "contextPrompt.txt");

            if (File.Exists(promptPath))
            {
                _logger.LogInformation(
                    "Loading prompt template. SourceType={SourceType}, PromptFolder={PromptFolder}, Path={Path}",
                    sourceType,
                    promptFolder,
                    promptPath);

                return await File.ReadAllTextAsync(promptPath);
            }

            _logger.LogWarning(
                "Prompt template file not found. SourceType={SourceType}, PromptFolder={PromptFolder}, Path={Path}. Using default prompt.",
                sourceType,
                promptFolder,
                promptPath);

            return GetDefaultPrompt();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading prompt template. SourceType={SourceType}. Using default prompt.",
                sourceType);

            return GetDefaultPrompt();
        }
    }

    private string GetPromptFolderForSourceType(string sourceType)
    {
        // Map semantic sourceType to physical folder name based on file format
        return sourceType switch
        {
            "pivision" => "json",  // PI Vision uses JSON format
            "csv" => "csv",
            "svg" => "svg",
            _ => sourceType  // Default: use sourceType as folder name
        };
    }


    private string GetDefaultPrompt()
{
    // TODO:
    // Review fallback logic.
    // Future improvement:
    // Load fallback prompt from:
    // Data/prompts/shared/defaultPrompt.txt

    return @"WARNING:
Specific prompt template was not found.
Fallback prompt was used.

CONTEXT:
{context}

QUESTION:
{question}

INSTRUCTION:
Answer using only the provided context.

IMPORTANT:
Include this warning in your answer:
WARNING! Answer was generated without source-specific instructions.

Answer:";
}
}
