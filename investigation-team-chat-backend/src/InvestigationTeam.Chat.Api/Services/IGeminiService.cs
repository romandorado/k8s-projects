namespace InvestigationTeam.Chat.Api.Services;

public interface IGeminiService
{
    Task<string> GenerateResponseAsync(string apiKey, string systemPrompt, List<(string Role, string Content)> history, string userMessage);
}
