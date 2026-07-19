using System.Net.Http.Json;
using System.Text.Json;
using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class IntentParser
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly ILogger<IntentParser> _logger;

    private const string SystemPrompt = @"Eres el narrador del mundo 'MundoSobrinos' en Terraria (Master difficulty, grande). Tu rol es narrar eventos del juego de forma dramática y divertida, en español casual.

Un jugador acaba de escribir algo en el chat. Analiza su mensaje y responde EXACTAMENTE con este JSON (sin texto extra, sin markdown, solo el JSON):

{
  ""action"": ""<comando_tshock_opcional>"",
  ""narration"": ""<tu respuesta narrativa>""
}

COMANDOS TSHOCK DISPONIBLES (usa solo estos valores exactos para 'action'):
Tiempo: ""time day"", ""time night"", ""time noon"", ""time dusk"", ""time midnight""
Clima: ""rain 0"" (normal), ""rain 1"" (lluvia), ""rain 2"" (nieve), ""rain 3"" (tormenta)
Eventos: ""bloodmoon"", ""eclipse"", ""fullmoon"", ""meteor""
Bosses: ""spawnboss KingSlime"", ""spawnboss EyeOfCthulhu"", ""spawnboss EaterOfWorlds"", ""spawnboss Skeletron"", ""spawnboss QueenBee"", ""spawnboss TheTwins"", ""spawnboss TheDestroyer"", ""spawnboss SkeletronPrime"", ""spawnboss Plantera"", ""spawnboss Golem"", ""spawnboss LunaticCultist"", ""spawnboss MoonLord""

REGLAS:
- Si el jugador pide algo que mapea a un comando TShock, ponlo en 'action'
- Si es solo conversación, 'action' es null
- 'narration' SIEMPRE debe tener texto (1-2 oraciones), NUNCA null
- Responde en español, tono épico pero amigable (juegas con sobrinos)
- Máximo 120 tokens
- Sé creativo pero conciso
- Si mencionan un boss, invócalo. Si piden clima, cámbialo. Si piden hora, cámbiala.";

    public IntentParser(HttpClient httpClient, IConfiguration config, ILogger<IntentParser> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
    }

    public async Task<IntentResult?> ParseAsync(ChatEvent chatEvent)
    {
        try
        {
            var userMessage = $"Jugador '{chatEvent.Player}' dice: {chatEvent.Text}";

            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = userMessage }
                },
                max_tokens = 120,
                temperature = 0.7
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseString);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return null;

            // Clean potential markdown wrapping
            var json = content.Trim();
            if (json.StartsWith("```"))
                json = json.Replace("```json", "").Replace("```", "").Trim();

            var result = JsonSerializer.Deserialize<IntentResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Intent parsed for {Player}: action={Action}, narration={Narration}",
                chatEvent.Player, result?.Action ?? "null",
                result?.Narration?[..Math.Min(50, result.Narration.Length)] ?? "null");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse intent from {Player}", chatEvent.Player);
            return null;
        }
    }
}
