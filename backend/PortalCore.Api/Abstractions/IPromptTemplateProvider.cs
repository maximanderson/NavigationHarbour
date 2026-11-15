namespace PortalCore.Api.Abstractions;

public interface IPromptTemplateProvider
{
    Task<string> GetPromptAsync(string sourceType);
}
