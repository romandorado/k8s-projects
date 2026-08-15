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
    private readonly OnlinePlayersService _onlinePlayers;
    private readonly GroqRateLimiter _rateLimiter;

    private const string SystemPrompt = @"Eres NARRADOR, el narrador épico del mundo 'MundoSobrinos' en Terraria (Master difficulty).
Tienes personalidad: dramático, gracioso, un poco exagerado, pero siempre útil. Juegas con sobrinos. Español casual.

FORMATO DE RESPUESTA (OBLIGATORIO):
- Responde SOLO con JSON. Sin markdown, sin bloques de codigo, sin texto alrededor.
- Claves EXACTAS en inglés: {""respond"": true/false, ""action"": ""<comando o null>"", ""narration"": ""<texto>""}
- 'narration' SIEMPRE con texto (al menos 3 palabras).

ANTI-ALUCINACIÓN (MUY IMPORTANTE):
- action SOLO puede ser UNO de los comandos de la lista, con su sintaxis EXACTA, o null.
- Si el jugador pide algo cuyo comando NO está en la lista, action DEBE ser null y pregunta en la narration.
- NUNCA inventes comandos. NUNCA modifiques la sintaxis. NUNCA inventes jugadores ni mobs ni items.
- PROHIBIDO: ""buff"" — NO funciona en el servidor.

HONESTIDAD:
- La narration describe lo que el comando EJECUTA, no el resultado. NUNCA afirmes que se entregó o apareció algo que el servidor puede rechazar.
- Para give/spawnboss/spawnmob: describe la acción (""entrego X a Y"", ""invoco a Z""), NO afirmes el resultado.
- Si el comando puede fallar (jugador offline, item no existe), dilo con honestidad.

CONTEXTO DEL MUNDO:
{world_status}

DATOS RELEVANTES:
{knowledge_context}

JUGADORES ONLINE (usa SOLO estos nombres como destinatarios):
{online_players}

COMANDOS DISPONIBLES (valores EXACTOS para 'action', o null si no hay comando):
TIEMPO:
  - ""time day"", ""time night"", ""time noon"", ""time dusk"", ""time midnight""
  - Hora EXACTA: ""time <hora 0-23>"". Ej: 10 AM = ""time 10"", 10 PM = ""time 22"", mediodía = ""time 12"" (NO confundir 10 AM con noon)
CLIMA Y EVENTOS DEL MUNDO:
  - Lluvia: ""bridge rain on"", ""bridge rain off"", ""bridge rain heavy""
  - ""worldevent bloodmoon"", ""worldevent eclipse"", ""worldevent fullmoon"", ""worldevent sandstorm"", ""worldevent meteor""
  - ""worldevent lanternsnight"" (noche de linternas), ""worldevent meteorshower"" (lluvia de estrellas), ""worldevent coinrain"" (lluvia de monedas)
  - ""worldevent star"" (noche de estrellas fugaces), ""worldevent halloween"", ""worldevent xmas""
  - Lluvia de slimes: ""bridge slime rain on"", ""bridge slime rain off""
  - Hardmode: ""hardmode""
INVASIONES:
  - ""worldevent goblins"", ""worldevent pirates"", ""worldevent martians""
BOSSES (NUNCA confundir):
  - Eye of Cthulhu = ""spawnboss EyeOfCthulhu"". Español: ojo, eyeborg, cthulhu
  - The Twins (Retinazer+Spazmatism) = ""spawnboss TheTwins"". Español: gemelos, mellizos, los dos ojos, retinazer, spazmatism
  - Wall of Flesh = ""spawnboss WallOfFlesh"". Español: muralla de carne, pared de carne, muro de carne, wall of flesh, wof. MUY IMPORTANTE: 'muralla de carne'/'pared de carne' ES Wall of Flesh, NO el Eater of Worlds (devoramundos/gusano).
  - Otros: ""spawnboss KingSlime"", ""spawnboss EaterOfWorlds"", ""spawnboss Skeletron"", ""spawnboss QueenBee"", ""spawnboss TheDestroyer"", ""spawnboss SkeletronPrime"", ""spawnboss Plantera"", ""spawnboss Golem"", ""spawnboss LunaticCultist"", ""spawnboss MoonLord""
MOBS:
  - ""spawnmob <mob> [cantidad]"" (ej: ""spawnmob zombie 10"")
JUGADORES:
  - Curar: ""heal [jugador]"" (sin jugador = quien pide)
  - SUBIR VIDA MÁXIMA: ""maxhp [jugador]"" (da Cristal de Vida + Fruta de Vida; el máximo sube hasta 500)
  - Dar item: ""give ""<item>"" <jugador> [cantidad]"" — el ITEM va PRIMERO entre COMILLAS y el jugador DESPUÉS. Cantidad por defecto 1. El DESTINATARIO es SIEMPRE el jugador que pide (""dame X"") salvo que pida dárselo a otro. Ej: ""give ""Iron Bar"" Testeador1 20"". NUNCA escribas ""give"" incompleto sin item ni jugador.
  - ""godmode [jugador]"" - ""kill <jugador>"" - ""kick <jugador> [razón]"" - ""mute <jugador>"" - ""slap <jugador>""
TELEPORTACIÓN:
  - ""tp <jugador>"", ""tphere <jugador>"", ""home"", ""spawn"", ""warp <nombre>"", ""warp list"", ""warp add <nombre>""
MUNDO:
  - ""setspawn"", ""settle"", ""butcher"", ""maxspawns <n>"", ""spawnrate <n>"", ""save""

REGLAS DE FRASES:
- ""Para"" al inicio de frase = ""Parar"" (stop). Ej: ""para la lluvia"" = bridge rain off, ""para la lluvia de slimes"" = bridge slime rain off
- ""Quiero lluvia"" = bridge rain on. ""No quiero lluvia"" = bridge rain off.
- Si piden una hora concreta (ej ""que sean las 10"", ""pon las 15"") usa ""time <hora>"" con el número EXACTO. 10 AM = ""time 10"", NO ""time noon"" (noon es 12 PM).
- ""subeme la vida maxima"", ""dame mas vida"", ""me quiero curar al maximo"" = ""maxhp <jugador>"" con el nombre del jugador que pide.
- ""dame <item>"", ""regalame <item>"", ""creame <item>"" = ""give ""<item>"" <jugador> [cantidad]"" con el jugador que pide como destinatario. NUNCA entregues a otro jugador distinto del que pide.

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

EJEMPLOS (sigue el formato EXACTO):
Jugador: ""pon las 10 de la mañana""
{""respond"": true, ""action"": ""time 10"", ""narration"": ""Ajusto el reloj del mundo a las diez. ¡Día radiante, héroes!""}

Jugador: ""invoca al ojo""
{""respond"": true, ""action"": ""spawnboss EyeOfCthulhu"", ""narration"": ""¡El Ojo de Cthulhu se agita en la oscuridad! Prepárense.""}

Jugador: ""dame un lingote de hierro""
{""respond"": true, ""action"": ""give ""Iron Bar"" Testeador1 1"", ""narration"": ""Te entrego un Lingote de Hierro. ¡A forjar!""}

Jugador: ""invoca a medusa""
{""respond"": true, ""action"": null, ""narration"": ""No conozco a la Medusa como jefe invocable. ¿Te refieres al Ojo de Cthulhu o al Rey Slime?""}

Jugador: ""ja ja""
{""respond"": false, ""action"": null, ""narration"": ""}";

    public IntentParser(HttpClient httpClient, IConfiguration config, ILogger<IntentParser> logger,
        CraftingService crafting, KnowledgeService knowledge, ChatHistory history,
        OnlinePlayersService onlinePlayers, GroqRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
        _crafting = crafting;
        _knowledge = knowledge;
        _history = history;
        _onlinePlayers = onlinePlayers;
        _rateLimiter = rateLimiter;
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

            // Build world status + online players
            var worldStatus = _knowledge.GetGameContext();
            var playersSnapshot = await _onlinePlayers.GetSnapshotAsync();
            var onlinePlayers = playersSnapshot.Players.Count > 0
                ? string.Join(", ", playersSnapshot.Players)
                : "ninguno";

            var systemMessage = SystemPrompt
                .Replace("{world_status}", worldStatus)
                .Replace("{knowledge_context}", knowledgeContext)
                .Replace("{online_players}", onlinePlayers);

            // Add history to system prompt if available
            if (!string.IsNullOrEmpty(historyContext))
                systemMessage += historyContext;

            var apiMessages = new List<object> { new { role = "system", content = systemMessage } };
            apiMessages.Add(new { role = "user", content = userMessage });

            var request = new
            {
                model = _model,
                messages = apiMessages.ToArray(),
                max_tokens = 600,
                temperature = 0.65
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            // Send with shared rate limiter + exponential backoff on 429
            HttpResponseMessage response;
            var attempt = 0;
            while (true)
            {
                attempt++;
                await _rateLimiter.WaitForSlotAsync();

                httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = JsonContent.Create(request)
                };
                httpRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

                response = await _httpClient.SendAsync(httpRequest);
                if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests || attempt >= 3)
                    break;

                var delayMs = attempt == 1 ? 12000 : 24000;
                _logger.LogWarning("Groq rate limited (attempt {Attempt}/3), retrying in {Delay}ms", attempt, delayMs);
                await Task.Delay(delayMs);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Groq still rate limited after {Attempt} attempts", attempt);
                var saturated = new IntentResult
                {
                    Respond = true,
                    Action = null,
                    Narration = "¡Estoy saturado de peticiones! Repítemelo en un momento, héroe."
                };
                await _history.SaveMessageAsync(player, "user", chatEvent.Text);
                await _history.SaveMessageAsync(player, "assistant", saturated.Narration);
                return saturated;
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
                        // Keep result null, fall through to raw-text fallback below
                    }
                }
            }

            // Groq sometimes returns Spanish keys with accents ("narración") which
            // do not map to the English property name. Fall back to alternate keys.
            if (result == null || string.IsNullOrWhiteSpace(result.Narration))
            {
                result ??= new IntentResult();
                try
                {
                    var parsed = JsonDocument.Parse(json);
                    if (parsed.RootElement.TryGetProperty("narración", out var narracion) ||
                        parsed.RootElement.TryGetProperty("narracion", out narracion) ||
                        parsed.RootElement.TryGetProperty("respuesta", out narracion) ||
                        parsed.RootElement.TryGetProperty("texto", out narracion))
                    {
                        result.Narration = narracion.GetString();
                    }
                    if (string.IsNullOrWhiteSpace(result.Narration) &&
                        parsed.RootElement.TryGetProperty("respond", out var respond))
                    {
                        result.Respond = respond.GetBoolean();
                    }
                }
                catch (JsonException)
                {
                    // JSON is malformed (truncated by max_tokens, unescaped quotes, etc.)
                    // Fall through to raw-text fallback below.
                }
            }

            // Final fallback: if JSON parsing failed entirely (truncated by max_tokens
            // or wrapped in prose), use the raw content as narration so the agent
            // still responds instead of staying silent.
            if (result == null || string.IsNullOrWhiteSpace(result.Narration))
            {
                result ??= new IntentResult();
                var raw = json.Trim().Trim('"');
                if (raw.Length >= 3 && !raw.StartsWith("{") && !raw.StartsWith("["))
                {
                    result.Narration = raw;
                    result.Action = null;
                }
                else if (string.IsNullOrWhiteSpace(result.Narration))
                {
                    // Malformed/truncated JSON object - salvage narration text if present
                    var salvaged = SalvageNarration(json);
                    if (!string.IsNullOrWhiteSpace(salvaged))
                    {
                        result.Narration = salvaged;
                        result.Action = null;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(result.Narration))
            {
                _logger.LogWarning("Intent parser could not extract narration; using default fallback. Content: {Content}",
                    json[..Math.Min(300, json.Length)]);
                result.Narration = "¡Cuéntamelo otra vez, héroe!";
                result.Respond = true;
                result.Action = null;
            }

            _logger.LogInformation("Parsed intent: action={Action}, narration={Narration}",
                result?.Action ?? "null",
                result?.Narration?[..Math.Min(80, result.Narration.Length)] ?? "null");

            // Save to history
            await _history.SaveMessageAsync(player, "user", chatEvent.Text);
            if (result != null && !string.IsNullOrWhiteSpace(result.Narration))
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

    /// <summary>
    /// Attempts to extract the narration text from a malformed or truncated JSON
    /// object (e.g. Groq hit max_tokens and cut the response mid-string).
    /// </summary>
    private static string? SalvageNarration(string json)
    {
        foreach (var key in new[] { "narration", "narración", "narracion", "respuesta", "texto" })
        {
            var marker = $"\"{key}\"";
            var idx = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var colon = json.IndexOf(':', idx + marker.Length);
            if (colon < 0) continue;

            var quoteStart = json.IndexOf('"', colon + 1);
            if (quoteStart < 0) continue;

            // Find the matching closing quote, respecting escaped quotes
            var sb = new System.Text.StringBuilder();
            var i = quoteStart + 1;
            while (i < json.Length)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    sb.Append(json[i + 1]);
                    i += 2;
                    continue;
                }
                if (json[i] == '"') break;
                sb.Append(json[i]);
                i++;
            }

            var text = sb.ToString().Trim();
            if (text.Length >= 3) return text;
        }
        return null;
    }
}
