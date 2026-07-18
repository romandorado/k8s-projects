using System.Net.Http.Json;
using System.Text.Json;
using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class GroqService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly ILogger<GroqService> _logger;

    private const string SystemPrompt = @"Eres el narrador del mundo 'MundoSobrinos' en Terraria. Tu rol:
- Narrar eventos del juego de forma dramática y divertida
- Responder a comandos de jugadores con creatividad
- Mantener el tono épico pero amigable (juegas con sobrinos)
- Usar español casual y emojis ocasionales

Contexto del mundo:
- Mundo: MundoSobrinos (Master, grande)
- Jugadores: [se actualiza dinámicamente]
- Hora del día: [se actualiza]
- Clima actual: [se actualiza]

Reglas:
- Máximo 200 tokens por respuesta
- Responde en español
- Sé conciso (1-2 oraciones máximo)
- Recuerda: en Master los enemigos son brutales, drops exclusivos, y la dificultad es extrema";

    public GroqService(HttpClient httpClient, IConfiguration config, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
    }

    public async Task<string> GenerateNarrationAsync(string userMessage, string context = "")
    {
        try
        {
            var systemMessage = string.IsNullOrEmpty(context) 
                ? SystemPrompt 
                : $"{SystemPrompt}\n\nContexto actual: {context}";

            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = userMessage }
                },
                max_tokens = 200,
                temperature = 0.8
            };

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.PostAsJsonAsync(_endpoint, request);
            var responseString = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(responseString);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            _logger.LogInformation("Groq narration generated: {Content}", content);
            return content ?? "El narrador está pensando...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate narration from Groq");
            return "El narrador está temporalmente silencioso...";
        }
    }
}
