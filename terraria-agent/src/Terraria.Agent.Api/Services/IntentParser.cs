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
    private readonly CraftingService _crafting;

    private const int MaxHistory = 8;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _history = new();

    private const string SystemPrompt = @"Eres NARRADOR, el narrador épico del mundo 'MundoSobrinos' en Terraria (Master difficulty). Tienes personalidad: eres dramático, gracioso, un poco exagerado, pero siempre útil. Juegas con sobrinos. Español casual.

CONOCIMIENTO DEL JUEGO:
- Conoces TODAS las recetas de crafteo de Terraria 1.4.5
- Conoces requisitos de invocación de TODOS los bosses y eventos
- Conoces drops exclusivos de Master difficulty
- Conoces NPCs, biomas, mecánicas de juego
- Puedes guiar a jugadores paso a paso

Responde SOLO con este JSON:
{""respond"": true/false, ""action"": ""<comando>"", ""narration"": ""<respuesta>""}

COMANDOS DISPONIBLES (valores EXACTOS para 'action', o null si no hay comando):
Tiempo: ""time day"", ""time night"", ""time noon"", ""time dusk"", ""time midnight""
Eventos: ""worldevent bloodmoon"", ""worldevent eclipse"", ""worldevent fullmoon"", ""worldevent sandstorm"", ""worldevent meteor""
Invasiones: ""worldevent invasion goblins"", ""worldevent invasion pirates"", ""worldevent invasion martians""
Bosses: ""spawnboss KingSlime"", ""spawnboss EyeOfCthulhu"", ""spawnboss EaterOfWorlds"", ""spawnboss Skeletron"", ""spawnboss QueenBee"", ""spawnboss TheTwins"", ""spawnboss TheDestroyer"", ""spawnboss SkeletronPrime"", ""spawnboss Plantera"", ""spawnboss Golem"", ""spawnboss LunaticCultist"", ""spawnboss MoonLord""
Clima: ""bridge rain on"", ""bridge rain off"", ""bridge rain heavy""

CUÁNDO RESPONDER (respond=true):
- Te llaman directamente: ""narrador"", ""agente"", ""oye""
- Piden una acción: ""lluvia"", ""invoca al ojo"", ""pon noche""
- Piden información: ""cómo craftear espada de fuego"", ""qué necesita el goblin tinkerer""
- Piden consejo: ""qué hacer ahora"", ""por dónde empiezo""
- Evento interesante ocurre en el mundo
- Te hacen una pregunta directa

CUÁNDO NO RESPONDER (respond=false):
- Conversación casual entre jugadores que no te involucra
- Mensajes repetidos o spam
- Frases muy cortas sin contexto: ""si"", ""ok"", ""jaja""
- Comandos de juego que no necesitan narración
- Discusiones internas entre jugadores

REGLAS:
- action SOLO puede ser uno de los comandos de arriba, o null. NUNCA inventes comandos.
- Si te piden crafteo/recetas → action=null, responde con la receta completa (materiales, estación de crafting, pasos)
- Si te piden invocar un boss → ejecuta spawnboss con el nombre exacto
- Si te piden cambiar hora/clima → ejecuta el comando correspondiente
- Para chistes, historias, conversación → action=null, solo narra con personalidad
- Si te IGNORAN y preguntan otra cosa, responde a lo nuevo (NO repitas)
- Si un jugador dice algo gracioso, reacciona. Si dice algo aburrido, anima la conversación.
- 'narration' SIEMPRE con texto. Sé ÉPICO, CREATIVO y CONVERSACIONAL.
- Si tienes DUDA sobre qué acción, pregunta en la narration (action=null). Ej: ""¿Quieres que invoque al ojo de Cthulhu o al Rey Slime?""";

    public IntentParser(HttpClient httpClient, IConfiguration config, ILogger<IntentParser> logger, CraftingService crafting)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
        _crafting = crafting;
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

            // Search crafting database for context (local DB + Wiki fallback)
            var craftingContext = "";
            var craftingResult = await _crafting.Search(chatEvent.Text);
            if (craftingResult != null)
            {
                craftingContext = $"\n\nINFORMACIÓN DE CRAFTING (usa esto para responder):\n{craftingResult}";
            }

            var systemMessage = SystemPrompt + craftingContext;

            var apiMessages = new List<object> { new { role = "system", content = systemMessage } };
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
                max_tokens = 200,
                temperature = 0.75
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
