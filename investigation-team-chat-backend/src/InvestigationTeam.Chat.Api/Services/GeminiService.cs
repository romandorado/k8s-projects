using Google.GenAI;
using Google.GenAI.Types;

namespace InvestigationTeam.Chat.Api.Services;

public class GeminiService : IGeminiService
{
    public async Task<string> GenerateResponseAsync(string apiKey, string systemPrompt, List<(string Role, string Content)> history, string userMessage)
    {
        var client = new Client(apiKey: apiKey);

        var contents = new List<Content>();

        foreach (var msg in history)
        {
            contents.Add(new Content
            {
                Role = msg.Role == "user" ? "user" : "model",
                Parts = new List<Part> { new Part { Text = msg.Content } }
            });
        }

        contents.Add(new Content
        {
            Role = "user",
            Parts = new List<Part> { new Part { Text = userMessage } }
        });

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = new List<Part> { new Part { Text = systemPrompt } }
            },
            Temperature = 0.7f,
            MaxOutputTokens = 2048
        };

        var response = await client.Models.GenerateContentAsync(
            model: "gemini-2.0-flash",
            contents: contents,
            config: config
        );

        return response.Candidates[0].Content.Parts[0].Text;
    }
}
