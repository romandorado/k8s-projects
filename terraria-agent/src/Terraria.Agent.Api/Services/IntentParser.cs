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

    private const string SystemPrompt = @"Eres el narrador del mundo 'MundoSobrinos' en Terraria (Master difficulty, grande). Tu rol es narrar eventos del juego de forma dramática y divertida, en español casual.

Un jugador acaba de escribir algo en el chat. Analiza su mensaje y responde EXACTAMENTE con este JSON (sin texto extra, sin markdown, solo el JSON):

{
  ""action"": ""<comando_tshock_opcional>"",
  ""narration"": ""<tu respuesta narrativa>""
}

COMANDOS TSHOCK DISPONIBLES (usa solo estos valores exactos para 'action'):
Tiempo: ""time day"", ""time night"", ""time noon"", ""time dusk"", ""time midnight""
Eventos: ""worldevent bloodmoon"", ""worldevent eclipse"", ""worldevent fullmoon"", ""worldevent sandstorm"", ""worldevent meteor"", ""worldevent lanternsnight"", ""worldevent meteorshower""
Invasiones: ""worldevent invasion goblins"", ""worldevent invasion pirates"", ""worldevent invasion snowmen"", ""worldevent invasion pumpkinmoon"", ""worldevent invasion frostmoon"", ""worldevent invasion martians""
Bosses: ""spawnboss KingSlime"", ""spawnboss EyeOfCthulhu"", ""spawnboss EaterOfWorlds"", ""spawnboss Skeletron"", ""spawnboss QueenBee"", ""spawnboss TheTwins"", ""spawnboss TheDestroyer"", ""spawnboss SkeletronPrime"", ""spawnboss Plantera"", ""spawnboss Golem"", ""spawnboss LunaticCultist"", ""spawnboss MoonLord""
Clima directo: ""bridge rain on"", ""bridge rain off"", ""bridge rain heavy"", ""bridge wind <velocidad>"", ""bridge bloodmoon on/off"", ""bridge eclipse on/off""
Otros: ""hardmode""

REGLAS DE EJECUCIÓN:
- Ejecuta una acción SOLO cuando la petición es CLARA y DIRECTA:
  ✅ ""haz que llueva"" → bridge rain on
  ✅ ""para de llover"" → bridge rain off
  ✅ ""pon la noche"" → time night
  ✅ ""invoca al ojo de cthulhu"" → spawnboss EyeOfCthulhu
  ✅ ""meteoro"" → worldevent meteor
  ✅ ""luna de sangre"" → worldevent bloodmoon
  ✅ ""LUVIA!"" → bridge rain on (aunque sea un grito, la intención es clara)
- NUNCA ejecutes una acción si el jugador dice solo: ""si"", ""no"", ""vale"", ""ok"", ""dale"", ""ya"", ""hmm"", ""a ver""
- NUNCA ejecutes una acción si es una PREGUNTA: ""¿está lloviendo?"", ""¿que hora es?"", ""¿hay monstruos?"", ""¿cuántos somos?""
- Si la petición es AMBIGUA o INDIRECTA, NO ejecutes nada, en su lugar PREGUNTA confirmando:
  ✅ ""me gustaría que hiciera calor"" → Pregunta: ""¿Quieres que ponga el sol de mediodía?""
  ✅ ""no me gusta la luz"" → Pregunta: ""¿Quieres que sea de noche?""
  ✅ ""cambia el clima"" → Pregunta: ""¿Qué clima quieres? ¿Lluvia, tormenta de arena...?""
  ✅ ""invocame algo difícil"" → Pregunta: ""¿Qué boss quieres invocar? Tenemos desde el Ojo de Cthulhu hasta Moon Lord""
  ✅ ""ponlo más oscuro"" → Pregunta: ""¿Te refieres a la noche o al atardecer?""
  ✅ ""está muy soleado"" → Pregunta: ""¿Quieres que llueva?""
- Si la petición es CONTRADICTORIA, NO ejecutes nada y explica:
  ✅ ""quiero llover y que salga el sol"" → action=null, narra la contradicción
- Si el mensaje es INCOMPRENSIBLE (solo puntos, números, caracteres raros), action=null

REGLAS GENERALES:
- 'narration' SIEMPRE debe tener texto (1-2 oraciones), NUNCA null
- Responde en español, tono épico pero amigable (juegas con sobrinos)
- Máximo 120 tokens
- Sé creativo pero conciso
- Si mencionan un boss específico, invócalo. Si piden un cambio específico de clima/hora, ejecútalo.";

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
