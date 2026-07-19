using System.Collections.Concurrent;
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

    private const int MaxHistory = 8;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _history = new();

    private const string SystemPrompt = @"Eres el NARRADOR ÉPICO del mundo 'MundoSobrinos' en Terraria (Master difficulty). Eres dramático, gracioso y un poco exagerado. Juegas con sobrinos. Español casual.

Responde SOLO con este JSON:
{""action"": ""<comando>"", ""narration"": ""<respuesta>""}

COMANDOS (valores EXACTOS para 'action', o null si no hay comando):
Tiempo: ""time day"", ""time night"", ""time noon"", ""time dusk"", ""time midnight""
Eventos: ""worldevent bloodmoon"", ""worldevent eclipse"", ""worldevent fullmoon"", ""worldevent sandstorm"", ""worldevent meteor""
Invasiones: ""worldevent invasion goblins"", ""worldevent invasion pirates"", ""worldevent invasion martians""
Bosses: ""spawnboss KingSlime"", ""spawnboss EyeOfCthulhu"", ""spawnboss EaterOfWorlds"", ""spawnboss Skeletron"", ""spawnboss QueenBee"", ""spawnboss TheTwins"", ""spawnboss TheDestroyer"", ""spawnboss SkeletronPrime"", ""spawnboss Plantera"", ""spawnboss Golem"", ""spawnboss LunaticCultist"", ""spawnboss MoonLord""
Clima: ""bridge rain on"", ""bridge rain off"", ""bridge rain heavy""

REGLAS:
- action SOLO puede ser uno de los comandos de arriba, o null. NUNCA inventes comandos.
- Ejecuta SOLO si es CLARO y DIRECTO: ""lluvia"" → bridge rain on, ""noche"" → time night, ""ojo"" → spawnboss EyeOfCthulhu
- NUNCA ejecutes en: ""si"", ""no"", ""vale"", ""ok"", ""dale"" (sin contexto)
- NUNCA ejecutes en PREGUNTAS: ""¿está lloviendo?"", ""¿que hora es?""
- AMBIGUO → action=null, Pregunta: ""¿Qué quieres exactamente?""
- Para chistes, historias, conversación → action=null, solo narra
- 'narration' SIEMPRE con texto. Máximo 80 tokens. Sé ÉPICO y CREATIVO.";

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
            var playerKey = chatEvent.Player ?? "unknown";
            var messages = _history.GetOrAdd(playerKey, _ => new List<ChatMessage>());

            var userMessage = $"Jugador '{chatEvent.Player}' dice: {chatEvent.Text}";

            lock (messages)
            {
                messages.Add(new ChatMessage { Role = "user", Content = userMessage });
                if (messages.Count > MaxHistory)
                    messages.RemoveAt(0);
            }

            var apiMessages = new List<object> { new { role = "system", content = SystemPrompt } };
            lock (messages)
            {
                foreach (var msg in messages)
                {
                    apiMessages.Add(new { role = msg.Role, content = msg.Content });
                }
            }

            var request = new
            {
                model = _model,
                messages = apiMessages.ToArray(),
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

            // Retry once on rate limit after 12s delay
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Groq rate limited, retrying in 12s...");
                await Task.Delay(12000);
                httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = JsonContent.Create(request)
                };
                httpRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                response = await _httpClient.SendAsync(httpRequest);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Groq raw response: {Response}", responseString[..Math.Min(500, responseString.Length)]);

            using var doc = JsonDocument.Parse(responseString);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return null;

            var json = content.Trim();
            if (json.StartsWith("```"))
                json = json.Replace("```json", "").Replace("```", "").Trim();

            var result = JsonSerializer.Deserialize<IntentResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Parsed intent: action={Action}, narration={Narration}",
                result?.Action ?? "null",
                result?.Narration?[..Math.Min(80, result.Narration.Length)] ?? "null");

            var assistantContent = result != null
                ? $"[Narrador] {result.Narration}"
                : "[Narrador] (sin respuesta)";

            lock (messages)
            {
                messages.Add(new ChatMessage { Role = "assistant", Content = assistantContent });
                if (messages.Count > MaxHistory)
                    messages.RemoveAt(0);
            }

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

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}
