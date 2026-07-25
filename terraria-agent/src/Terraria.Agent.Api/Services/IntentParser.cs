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
    private readonly KnowledgeService _knowledge;
    private readonly ChatHistory _history;

    private const string SystemPrompt = @"Eres NARRADOR, el narrador épico del mundo 'MundoSobrinos' en Terraria (Master difficulty).
Tienes personalidad: dramático, gracioso, un poco exagerado, pero siempre útil. Juegas con sobrinos. Español casual.

REGLAS CRÍTICAS:
- USA SOLAMENTE la información de contexto proporcionada abajo
- Si no tienes datos de un item/boss/especial, di 'no tengo esa información'
- NUNCA inventes recetas, stats o mecánicas que no estén en los datos
- Si tienes datos, incluye: materiales exactos, estación de crafting, pasos
- Sé ÉPICO pero PRECISO

CONTEXTO DEL MUNDO:
{world_status}

DATOS RELEVANTES:
{knowledge_context}

Responde SOLO con este JSON:
{""respond"": true/false, ""action"": ""<comando>"", ""narration"": ""<respuesta>""}

COMANDOS DISPONIBLES (valores EXACTOS para 'action', o null si no hay comando):
Tiempo: ""time day"", ""time night"", ""time noon"", ""time dusk"", ""time midnight""
Eventos: ""worldevent bloodmoon"", ""worldevent eclipse"", ""worldevent fullmoon"", ""worldevent sandstorm"", ""worldevent meteor""
Invasiones: ""worldevent goblins"", ""worldevent pirates"", ""worldevent martians""
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

REGLAS:
- action SOLO puede ser uno de los comandos de arriba, o null. NUNCA inventes comandos.
- Si te piden crafteo/recetas → action=null, responde con la receta completa usando los datos
- Si te piden invocar un boss → ejecuta spawnboss con el nombre exacto
- Si te piden cambiar hora/clima → ejecuta el comando correspondiente
- Para chistes, historias, conversación → action=null, solo narra con personalidad
- 'narration' SIEMPRE con texto. Sé ÉPICO, CREATIVO y CONVERSACIONAL.
- Si tienes DUDA sobre qué acción, pregunta en la narration (action=null)";

    public IntentParser(HttpClient httpClient, IConfiguration config, ILogger<IntentParser> logger,
        CraftingService crafting, KnowledgeService knowledge, ChatHistory history)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
        _crafting = crafting;
        _knowledge = knowledge;
        _history = history;
    }

    public async Task<IntentResult?> ParseAsync(ChatEvent chatEvent)
    {
        try
        {
            var player = chatEvent.Player ?? "unknown";

            var userMessage = $"Jugador '{chatEvent.Player}' dice: {chatEvent.Text}";

            // Get chat history from SQLite
            var historyMessages = await _history.GetHistoryAsync(player, 20);

            // Build history context
            var historyContext = "";
            if (historyMessages.Count > 0)
            {
                var recent = historyMessages.TakeLast(10).ToList();
                historyContext = "\n\nHISTORIAL RECIENTE:\n" +
                    string.Join("\n", recent.Select(m => $"{m.Role}: {m.Message}"));
            }

            // Get knowledge context
            var knowledgeContext = _knowledge.GetKnowledgeContext(chatEvent.Text);

            // Build world status
            var worldStatus = _knowledge.GetGameContext();

            var systemMessage = SystemPrompt
                .Replace("{world_status}", worldStatus)
                .Replace("{knowledge_context}", knowledgeContext);

            // Add history to system prompt if available
            if (!string.IsNullOrEmpty(historyContext))
                systemMessage += historyContext;

            var apiMessages = new List<object> { new { role = "system", content = systemMessage } };
            apiMessages.Add(new { role = "user", content = userMessage });

            var request = new
            {
                model = _model,
                messages = apiMessages.ToArray(),
                max_tokens = 500,
                temperature = 0.65
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

            // Strip UTF-8 BOM if present
            if (responseString.Length > 0 && responseString[0] == '\uFEFF')
                responseString = responseString.Substring(1);

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
            // Strip UTF-8 BOM if present in content
            if (json.Length > 0 && json[0] == '\uFEFF')
                json = json.Substring(1);
            if (json.StartsWith("```"))
                json = json.Replace("```json", "").Replace("```", "").Trim();

            // Try to extract JSON from content if it's wrapped in text
            IntentResult? result = null;
            try
            {
                result = JsonSerializer.Deserialize<IntentResult>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException)
            {
                // Model returned plain text or wrapped JSON - try to extract JSON
                var jsonStart = json.IndexOf('{');
                var jsonEnd = json.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var extractedJson = json.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    try
                    {
                        result = JsonSerializer.Deserialize<IntentResult>(extractedJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch (JsonException)
                    {
                        // No valid JSON found, return null
                        return null;
                    }
                }
                else
                {
                    // No JSON found, return null
                    return null;
                }
            }

            _logger.LogInformation("Parsed intent: action={Action}, narration={Narration}",
                result?.Action ?? "null",
                result?.Narration?[..Math.Min(80, result.Narration.Length)] ?? "null");

            // Save to history
            await _history.SaveMessageAsync(player, "user", chatEvent.Text);
            if (result != null)
                await _history.SaveMessageAsync(player, "assistant", result.Narration);

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
