using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvestigationTeam.Chat.Api.Services;

public class GeminiService : IGeminiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private const string GroqBaseUrl = "https://api.groq.com/openai/v1";
    private const string DefaultModel = "llama-3.3-70b-versatile";

    public GeminiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GenerateResponseAsync(string apiKey, string systemPrompt, List<(string Role, string Content)> history, string userMessage)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var messages = new List<GroqMessage>();

        messages.Add(new GroqMessage { Role = "system", Content = systemPrompt });

        foreach (var msg in history)
        {
            messages.Add(new GroqMessage { Role = msg.Role == "user" ? "user" : "assistant", Content = msg.Content });
        }

        messages.Add(new GroqMessage { Role = "user", Content = userMessage });

        var request = new GroqCompletionRequest
        {
            Model = DefaultModel,
            Messages = messages,
            Temperature = 0.7,
            MaxTokens = 2048
        };

        try
        {
            var response = await client.PostAsJsonAsync($"{GroqBaseUrl}/chat/completions", request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = "Error desconocido de Groq";
                try
                {
                    var errorDoc = JsonDocument.Parse(body);
                    if (errorDoc.RootElement.TryGetProperty("error", out var errorProp) &&
                        errorProp.TryGetProperty("message", out var msgProp))
                    {
                        errorMsg = msgProp.GetString() ?? errorMsg;
                    }
                }
                catch { }
                throw new InvalidOperationException($"Error al llamar a Groq API: {errorMsg}");
            }

            var completion = JsonSerializer.Deserialize<GroqCompletionResponse>(body);
            return completion?.Choices?.FirstOrDefault()?.Message?.Content ?? "Respuesta vacía de Groq.";
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Error de conexión con Groq: {ex.Message}", ex);
        }
    }
}

public class GroqMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class GroqCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<GroqMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 2048;
}

public class GroqCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<GroqChoice>? Choices { get; set; }
}

public class GroqChoice
{
    [JsonPropertyName("message")]
    public GroqMessage? Message { get; set; }
}
