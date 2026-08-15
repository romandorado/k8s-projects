using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Terraria.Agent.Api.Models;
using Terraria.Agent.Api.Services;

namespace Terraria.Agent.Api.Controllers;

/// <summary>
/// Main chat endpoint for interacting with the Terraria Agent.
/// The agent acts as an epic narrator, processes natural language commands,
/// and executes TShock server commands.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly CommandParser _parser;
    private readonly TShockClient _tshock;
    private readonly GroqService _groq;
    private readonly IntentParser _intentParser;
    private readonly ChatHistory _history;
    private readonly ActionValidator _actionValidator;
    private readonly OnlinePlayersService _onlinePlayers;
    private readonly ILogger<ChatController> _logger;
    private readonly IConfiguration _config;
    private readonly bool _readOnly;

    private static readonly HashSet<string> IgnoreWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ok", "si", "no", "jaja", "jeje", "lol", "xd", "aja", "uh", "eh", "ah", "oh",
        "vale", "bien", "mal", "feo", "guay", "top", "gg", "wp", "gl", "hf", "brb", "afk",
        "xdxd", "jajaja", "jejeje", "hola", "adios", "bye", "chau"
    };

    private static readonly Dictionary<string, string> ClimateCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lluvia"] = "rain 1",
        ["nieve"] = "rain 2",
        ["tormenta"] = "rain 3",
        ["normal"] = "rain 0"
    };

    private static readonly Dictionary<string, string> TimeCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dia"] = "time day",
        ["day"] = "time day",
        ["noche"] = "time night",
        ["night"] = "time night",
        ["mediodia"] = "time noon",
        ["noon"] = "time noon",
        ["atardecer"] = "time dusk",
        ["dusk"] = "time dusk",
        ["medianoche"] = "time midnight",
        ["midnight"] = "time midnight"
    };

    private static readonly Dictionary<string, string> StopEventCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["para la lluvia de slimes"] = "bridge slime rain off",
        ["para la lluvia slimes"] = "bridge slime rain off",
        ["para lluvia de slimes"] = "bridge slime rain off",
        ["para slimes"] = "bridge slime rain off",
        ["para los slimes"] = "bridge slime rain off",
        ["para la lluvia"] = "bridge rain off",
        ["para el evento"] = "worldevent",
        ["no quiero lluvia"] = "bridge rain off",
        ["no quiero slimes"] = "bridge slime rain off"
    };

    private static readonly Dictionary<string, string> BossCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wall of flesh"] = "spawnboss WallOfFlesh",
        ["wall"] = "spawnboss WallOfFlesh",
        ["muro"] = "spawnboss WallOfFlesh",
        ["king slime"] = "spawnboss KingSlime",
        ["slime"] = "spawnboss KingSlime",
        ["slim"] = "spawnboss KingSlime",
        ["eye of cthulhu"] = "spawnboss EyeOfCthulhu",
        ["eye"] = "spawnboss EyeOfCthulhu",
        ["ojo"] = "spawnboss EyeOfCthulhu",
        ["eater of worlds"] = "spawnboss EaterOfWorlds",
        ["eater"] = "spawnboss EaterOfWorlds",
        ["gusano"] = "spawnboss EaterOfWorlds",
        ["skeletron"] = "spawnboss Skeletron",
        ["esqueleto"] = "spawnboss Skeletron",
        ["queen bee"] = "spawnboss QueenBee",
        ["bee"] = "spawnboss QueenBee",
        ["abeja"] = "spawnboss QueenBee",
        ["twins"] = "spawnboss TheTwins",
        ["gemelos"] = "spawnboss TheTwins",
        ["destroyer"] = "spawnboss TheDestroyer",
        ["destructor"] = "spawnboss TheDestroyer",
        ["prime"] = "spawnboss SkeletronPrime",
        ["skeletron prime"] = "spawnboss SkeletronPrime",
        ["primo"] = "spawnboss SkeletronPrime",
        ["plantera"] = "spawnboss Plantera",
        ["golem"] = "spawnboss Golem",
        ["lunatic"] = "spawnboss LunaticCultist",
        ["lunatic cultist"] = "spawnboss LunaticCultist",
        ["cultista"] = "spawnboss LunaticCultist",
        ["moon lord"] = "spawnboss MoonLord",
        ["moon"] = "spawnboss MoonLord",
        ["lord"] = "spawnboss MoonLord",
        ["señor"] = "spawnboss MoonLord",
        ["muralla de carne"] = "spawnboss WallOfFlesh",
        ["muralla"] = "spawnboss WallOfFlesh",
        ["pared de carne"] = "spawnboss WallOfFlesh",
        ["muro de carne"] = "spawnboss WallOfFlesh",
        ["pared"] = "spawnboss WallOfFlesh",
        ["wof"] = "spawnboss WallOfFlesh"
    };

    private static readonly string[] GiveTriggerPrefixes =
    {
        "dame", "regalame", "creame", "fabricame", "hazme"
    };

    private static readonly Dictionary<string, string> GiveItemAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ceniza"] = "Ash Block",
        ["ceni"] = "Ash Block",
        ["lingote de hierro"] = "Iron Bar",
        ["lingote hierro"] = "Iron Bar",
        ["lingote de cobre"] = "Copper Bar",
        ["lingote cobre"] = "Copper Bar",
        ["lingote de plata"] = "Silver Bar",
        ["lingote plata"] = "Silver Bar",
        ["lingote de oro"] = "Gold Bar",
        ["lingote oro"] = "Gold Bar",
        ["lingote de platino"] = "Platinum Bar",
        ["lingote platino"] = "Platinum Bar",
        ["lingote de tungsteno"] = "Tungsten Bar",
        ["lingote tungsteno"] = "Tungsten Bar",
        ["pocion de vida"] = "Healing Potion",
        ["poción de vida"] = "Healing Potion",
        ["pocion de curacion"] = "Healing Potion",
        ["poción de curación"] = "Healing Potion",
        ["pocion de salud"] = "Healing Potion",
        ["poción de salud"] = "Healing Potion",
        ["pocion"] = "Healing Potion",
        ["poción"] = "Healing Potion",
        ["cristal de vida"] = "Life Crystal",
        ["cristal"] = "Life Crystal",
        ["fruta de vida"] = "Life Fruit",
        ["espada de madera"] = "Wooden Sword",
        ["espada de cobre"] = "Copper Shortsword",
        ["espada"] = "Copper Shortsword"
    };

    public ChatController(
        CommandParser parser,
        TShockClient tshock,
        GroqService groq,
        IntentParser intentParser,
        ChatHistory history,
        ActionValidator actionValidator,
        OnlinePlayersService onlinePlayers,
        ILogger<ChatController> logger,
        IConfiguration config)
    {
        _parser = parser;
        _tshock = tshock;
        _groq = groq;
        _intentParser = intentParser;
        _history = history;
        _actionValidator = actionValidator;
        _onlinePlayers = onlinePlayers;
        _logger = logger;
        _config = config;
        _readOnly = config.GetValue<bool>("Agent:ReadOnly", false);
    }

    /// <summary>
    /// Get the chat history stored by the agent.
    /// </summary>
    /// <remarks>
    /// Returns messages oldest-first, optionally filtered by player, with
    /// cursor-based pagination via <paramref name="afterId"/>.
    ///
    /// Sample requests:
    /// - "GET /api/chat/history" → last 50 messages from all players
    /// - "GET /api/chat/history?player=Testeador1" → that player's messages
    /// - "GET /api/chat/history?limit=100" → up to 100 messages
    /// - "GET /api/chat/history?afterId=98" → messages after id 98
    /// </remarks>
    /// <param name="agentToken">Authentication token from X-Agent-Token header</param>
    /// <param name="player">Optional player name filter</param>
    /// <param name="limit">Max messages to return (default 50, max 500)</param>
    /// <param name="afterId">Return only messages with id greater than this (pagination)</param>
    /// <returns>Chat history as a list of messages</returns>
    /// <response code="200">Returns the chat history</response>
    /// <response code="401">If the agent token is invalid</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHistory(
        [FromHeader(Name = "X-Agent-Token")] string? agentToken,
        [FromQuery] string? player = null,
        [FromQuery] int limit = 50,
        [FromQuery] long afterId = 0)
    {
        var expectedToken = _config["Agent:Token"];
        if (string.IsNullOrEmpty(expectedToken) || agentToken != expectedToken)
            return Unauthorized();

        limit = Math.Clamp(limit, 1, 500);

        var messages = await _history.GetHistoryPageAsync(player, limit, afterId);
        var maxId = await _history.GetMaxIdAsync();

        return Ok(new
        {
            total = messages.Count,
            maxId = maxId,
            hasMore = messages.Count > 0 && messages[^1].Id < maxId,
            messages
        });
    }

    /// <summary>
    /// Send a chat message to the Terraria Agent.
    /// </summary>
    /// <remarks>
    /// The agent processes the message and can:
    /// - Execute game commands (time, weather, spawn boss)
    /// - Narrate events with epic descriptions
    /// - Answer questions about crafting, bosses, and game mechanics
    /// - Remember conversation history across restarts (SQLite)
    /// 
    /// Sample requests:
    /// - Natural language: "como se fabrica excalibur"
    /// - Command: "hora del dia"
    /// - Boss spawn: "invocar moon lord"
    /// - Narration: "narrar una tormenta se acerca"
    /// </remarks>
    /// <param name="agentToken">Authentication token from X-Agent-Token header</param>
    /// <param name="chatEvent">Chat message from player</param>
    /// <returns>Narration response with optional action executed</returns>
    /// <response code="200">Returns the narration response</response>
    /// <response code="401">If the agent token is invalid</response>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> HandleEvent(
        [FromHeader(Name = "X-Agent-Token")] string? agentToken,
        [FromBody] ChatEvent chatEvent)
    {
        var expectedToken = _config["Agent:Token"];
        if (string.IsNullOrEmpty(expectedToken) || agentToken != expectedToken)
            return Unauthorized();

        _logger.LogInformation("Chat from {Player}: {Text}", chatEvent.Player, chatEvent.Text);

        // Route 0: System events from plugin hooks (deaths, boss kills, joins, weather) - always narrate
        if (chatEvent.Player == "Sistema")
        {
            _logger.LogInformation("System event: {Text}", chatEvent.Text);
            var status = await _tshock.GetStatusAsync();
            var narration = await _groq.GenerateEventNarrationAsync(chatEvent.Text, status);
            await BroadcastMessageAsync($"[Narrador] {narration}");
            return Ok(new { narration = narration, systemEvent = true });
        }

        // Route 1: /agente commands (existing system)
        var command = _parser.Parse(chatEvent);
        if (command != null)
        {
            return await HandleAgentCommand(command);
        }

        // Filter out short/meaningless messages BEFORE calling Groq
        if (!ShouldRespond(chatEvent.Text))
        {
            _logger.LogInformation("Ignoring message from {Player}: too short or meaningless", chatEvent.Player);
            return Ok();
        }

        // Route 2a: Local command shortcuts (no Groq call)
        var localAction = GetLocalAction(chatEvent.Text);
        if (localAction != null)
        {
            _logger.LogInformation("Local action for {Player}: {Action}", chatEvent.Player, localAction);
            if (!_readOnly)
                await _tshock.ExecuteCommandAsync(localAction);
            var msg = localAction switch
            {
                "bridge slime rain off" => "¡La lluvia de slimes se detiene! El cielo se aclara.",
                "bridge rain off" => "La lluvia cesa. El sol vuelve a brillar.",
                _ => $"Comando ejecutado: {localAction}"
            };
            return Ok(new { narration = msg, action = localAction });
        }

        // Route 2b: Time query - report the real world time without changing it
        if (IsTimeQuery(chatEvent.Text))
        {
            _logger.LogInformation("Time query for {Player}: {Text}", chatEvent.Player, chatEvent.Text);
            var result = await _tshock.ExecuteCommandAsync("bridge time now");
            var timeText = ExtractTimeText(result);
            var msg = $"En el mundo ahora son las {timeText}.";
            await _history.SaveMessageAsync(chatEvent.Player, "user", chatEvent.Text);
            await _history.SaveMessageAsync(chatEvent.Player, "assistant", msg);
            await BroadcastMessageAsync($"[Narrador] {msg}");
            return Ok(new { narration = msg, action = "time now" });
        }

        // Route 2c: Local "dame/creame X" give requests (no Groq call, honest confirmation)
        var giveRequest = GetGiveRequest(chatEvent.Text, chatEvent.Player);
        if (giveRequest != null)
        {
            _logger.LogInformation("Give request for {Player}: item={Item}, target={Target}, qty={Qty}",
                chatEvent.Player, giveRequest.Item, giveRequest.Target, giveRequest.Quantity);
            return await HandleGiveAsync(chatEvent, giveRequest);
        }

        // Route 3: Natural language (IntentParser via Groq)
        var intent = await _intentParser.ParseAsync(chatEvent);
        if (intent == null || string.IsNullOrWhiteSpace(intent.Narration) || !intent.Respond)
        {
            _logger.LogInformation("IntentParser: no response for {Player} (respond={Respond})", chatEvent.Player, intent?.Respond);
            return Ok();
        }

        // Execute TShock action if detected
        var executedAction = intent.Action;
        if (!string.IsNullOrWhiteSpace(intent.Action))
        {
            if (_readOnly)
            {
                var honest = $"Estoy en modo solo lectura, no puedo ejecutar: {intent.Action}";
                _logger.LogInformation("Read-only mode: skipping action {Action}", intent.Action);
                await BroadcastMessageAsync($"[Narrador] {honest}");
                await _history.SaveMessageAsync(chatEvent.Player, "assistant", honest);
                return Ok(new { narration = honest, action = intent.Action, failure = true });
            }
            else
            {
                var validation = _actionValidator.Validate(intent.Action);

                if (!validation.IsValid)
                {
                    var honest = $"No puedo ejecutar eso. {validation.Reason}";
                    _logger.LogInformation("Rejected action {Action} from {Player}: {Reason}",
                        intent.Action, chatEvent.Player, validation.Reason);
                    await BroadcastMessageAsync($"[Narrador] {honest}");
                    await _history.SaveMessageAsync(chatEvent.Player, "assistant", honest);
                    return Ok(new { narration = honest, action = intent.Action, failure = true });
                }

                executedAction = validation.Action ?? string.Empty;

                if (!string.IsNullOrEmpty(validation.TargetPlayer))
                {
                    var snapshot = await _onlinePlayers.GetSnapshotAsync();
                    if (snapshot.IsReliable &&
                        !snapshot.Players.Contains(validation.TargetPlayer, StringComparer.OrdinalIgnoreCase))
                    {
                        var honest = $"{validation.TargetPlayer} no está conectado ahora mismo.";
                        _logger.LogInformation("Rejected action {Action} from {Player}: target {Target} offline",
                            intent.Action, chatEvent.Player, validation.TargetPlayer);
                        await BroadcastMessageAsync($"[Narrador] {honest}");
                        await _history.SaveMessageAsync(chatEvent.Player, "assistant", honest);
                        return Ok(new { narration = honest, action = executedAction, failure = true });
                    }
                }

                if (validation.Item != null)
                {
                    var req = new GiveRequest(validation.Item, validation.GiveTarget!, validation.Quantity);
                    return await HandleGiveAsync(chatEvent, req);
                }

                if (IsMaxHpAction(executedAction))
                {
                    var narration = await HandleMaxHpAsync(chatEvent, executedAction);
                    await BroadcastMessageAsync($"[Narrador] {narration}");
                    await _history.SaveMessageAsync(chatEvent.Player, "assistant", narration);
                    return Ok(new { narration = narration, action = executedAction });
                }

                _logger.LogInformation("Executing action: {Action}", executedAction);
                var response = await _tshock.ExecuteCommandAsync(executedAction);
                if (LooksLikeCommandFailure(response))
                {
                    _logger.LogWarning("Action {Action} reported failure: {Response}", executedAction, response);
                    var honest = $"Lo intenté, pero el servidor rechazó el comando. {intent.Narration}";
                    await BroadcastMessageAsync($"[Narrador] {honest}");
                    await _history.SaveMessageAsync(chatEvent.Player, "assistant", honest);
                    return Ok(new { narration = honest, action = executedAction, failure = true });
                }

                if (IsMechanical(executedAction) && IsGenericNarration(intent.Narration))
                    intent.Narration = $"Hecho: {executedAction}";
            }
        }

        // Broadcast narration
        await BroadcastMessageAsync($"[Narrador] {intent.Narration}");

        // Return narration in response body for testing/API consumers
        return Ok(new { narration = intent.Narration, action = executedAction });
    }

    private static readonly string[] FailureMarkers =
    {
        "invalid command", "invalid player", "invalid item", "invalid syntax",
        "you must use this command in-game", "not authorized", "unknown command",
        "player not found", "no such command", "cannot be found", "must use this command",
        "free slots", "banned items", "missing item", "missing player", "invalid item type",
        "more than one match found", "unable to decide", "no players online"
    };

    private static bool LooksLikeCommandFailure(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return false;
        var lower = response.ToLowerInvariant();
        return FailureMarkers.Any(lower.Contains);
    }

    private static bool IsMaxHpAction(string action)
    {
        var lower = action.TrimStart('/').ToLowerInvariant();
        return lower == "maxhp" || lower.StartsWith("maxhp ");
    }

    private static string? GetMaxHpPlayer(string action, string fallbackPlayer)
    {
        var lower = action.TrimStart('/');
        var parts = lower.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return fallbackPlayer;
        var candidate = parts[1].Trim().Trim('"');
        return string.IsNullOrWhiteSpace(candidate) ? fallbackPlayer : candidate;
    }

    private async Task<string> HandleMaxHpAsync(ChatEvent chatEvent, string action)
    {
        var target = GetMaxHpPlayer(action, chatEvent.Player);
        _logger.LogInformation("Max HP action for {Player}: target={Target}", chatEvent.Player, target);

        var crystalResult = await _tshock.ExecuteCommandAsync($"give \"Life Crystal\" {target} 10");
        var fruitResult = await _tshock.ExecuteCommandAsync($"give \"Life Fruit\" {target} 20");

        var crystalOk = crystalResult != null && !LooksLikeCommandFailure(crystalResult);
        var fruitOk = fruitResult != null && !LooksLikeCommandFailure(fruitResult);

        if (crystalOk || fruitOk)
        {
            var items = string.Join(" y ",
                new[] { crystalOk ? "10 Cristales de Vida" : null, fruitOk ? "20 Frutas de Vida" : null }
                    .Where(x => x != null));
            return $"He entregado {items} a {target}. Úsalos (clic derecho sobre ellos) para subir tu vida máxima hasta 500. ¡A por más corazones, héroe!";
        }

        return $"Lo intenté, pero el servidor no pudo entregarte los Cristales ni las Frutas de Vida, {target}. ¿Estás conectado?";
    }

    private async Task<IActionResult> HandleGiveAsync(ChatEvent chatEvent, GiveRequest request)
    {
        if (_readOnly)
        {
            var roMsg = "El narrador esta en modo solo lectura. No puedo entregar items.";
            return Ok(new { narration = roMsg });
        }

        var cmd = $"give \"{request.Item}\" {request.Target} {request.Quantity}";
        var response = await _tshock.ExecuteCommandAsync(cmd);

        if (LooksLikeCommandFailure(response))
        {
            _logger.LogWarning("Give action reported failure: {Response}", response);
            var honest = $"Lo intenté, pero el servidor rechazó entregar {request.Quantity} {request.Item} a {request.Target}. ¿Está conectado?";
            await BroadcastMessageAsync($"[Narrador] {honest}");
            return Ok(new { narration = honest, action = cmd, failure = true });
        }

        var confirmation = ExtractGiveConfirmation(response);
        var narration = confirmation != null
            ? $"¡Hecho! {confirmation}"
            : $"He entregado {request.Quantity} {request.Item} a {request.Target}. ¡A usarlos!";
        await BroadcastMessageAsync($"[Narrador] {narration}");
        return Ok(new { narration = narration, action = cmd });
    }

    private static GiveRequest? GetGiveRequest(string text, string fallbackPlayer)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var lower = text.Trim().ToLowerInvariant();
        lower = System.Text.RegularExpressions.Regex.Replace(lower, @"^(agente|narrador|oye|hola)[,\s]+", "").Trim();

        var prefix = GiveTriggerPrefixes.FirstOrDefault(p => lower.StartsWith(p, StringComparison.Ordinal));
        if (prefix == null) return null;

        var rest = lower[prefix.Length..].Trim();
        foreach (var article in new[] { "unos ", "unas ", "una ", "un ", "las ", "los ", "la ", "el " })
        {
            if (rest.StartsWith(article))
            {
                rest = rest[article.Length..].Trim();
                break;
            }
        }
        rest = rest.TrimEnd('.', '!', '?', ',');
        if (rest.Length < 2) return null;

        var quantity = 1;
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && int.TryParse(parts[0], out var q) && q > 0 && q <= 999)
        {
            quantity = q;
            rest = string.Join(" ", parts.Skip(1));
            if (rest.Length < 2) return null;
        }

        // Per-word singularization tries two candidates so both Spanish plural types work:
        // "pociones" -> "pocion" (base ends in consonant) and "lingotes" -> "lingote" (base ends in vowel).
        var words = rest.Split(' ');
        var matchableStripEs = string.Join(" ", words.Select(w =>
            w.EndsWith("es", StringComparison.Ordinal) && w.Length > 4 ? w[..^2]
            : w.EndsWith("s", StringComparison.Ordinal) && w.Length > 3 ? w[..^1]
            : w));
        var matchableStripS = string.Join(" ", words.Select(w =>
            (w.EndsWith("es", StringComparison.Ordinal) || w.EndsWith("s", StringComparison.Ordinal)) && w.Length > 3 ? w[..^1]
            : w));

        foreach (var matchable in new[] { matchableStripEs, matchableStripS }.OrderByDescending(m => m.Length))
        {
            foreach (var kvp in GiveItemAliases.OrderByDescending(k => k.Key.Length))
            {
                if (matchable.Contains(kvp.Key))
                    return new GiveRequest(kvp.Value, fallbackPlayer, quantity);
            }
        }
        return null;
    }

    private static string? ExtractGiveConfirmation(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;
        var idx = response.IndexOf("Gave ", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var end = response.IndexOf('.', idx);
        var text = end > idx ? response[idx..end] : response[idx..];
        text = text.Trim().Trim('"', '}', '{', '\n', '\r');
        return text.Length >= 4 ? text + "." : null;
    }

    private sealed record GiveRequest(string Item, string Target, int Quantity);

    private static string? GetLocalAction(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var lower = text.Trim().ToLowerInvariant();
        foreach (var kvp in StopEventCommands)
        {
            if (lower.Contains(kvp.Key))
                return kvp.Value;
        }
        return null;
    }

    private static readonly string[] TimeQueryPatterns =
    {
        "que hora es", "qué hora es", "dime la hora", "hora actual", "hora del mundo",
        "que hora", "qué hora", "la hora", "hora real", "cuantos son", "cuántos son",
        "hora" 
    };

    private static bool IsTimeQuery(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.Trim().ToLowerInvariant();
        // Ignore time-setting phrases that also contain "hora"
        if (lower.Contains("cambia") || lower.Contains("pon las") || lower.Contains("haz") ||
            lower.Contains("que sea") || lower.Contains("setea") || lower.Contains("a las"))
            return false;
        return TimeQueryPatterns.Any(p => lower == p || lower.Contains(p));
    }

    private static string ExtractTimeText(string? result)
    {
        if (string.IsNullOrWhiteSpace(result)) return "desconocida";
        var idx = result.IndexOf("current time is ", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var time = result[(idx + "current time is ".Length)..].Trim().Trim('"', '}', '{');
            // Strip any trailing JSON
            var quote = time.IndexOf('"');
            if (quote > 0) time = time[..quote];
            return time.Trim();
        }
        // Fallback: TShock rawcmd response format "The current time is 4:08."
        idx = result.IndexOf("current time is", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var time = result[(idx + "current time is".Length)..].Trim().Trim('"', '.');
            return time.Trim();
        }
        return "desconocida";
    }

    private static bool ShouldRespond(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();

        // Too short
        if (trimmed.Length < 3) return false;

        // Common filler/noise words
        if (IgnoreWords.Contains(trimmed)) return false;

        // Just punctuation or emojis
        var stripped = System.Text.RegularExpressions.Regex.Replace(trimmed.ToLowerInvariant(), @"[^\w]", "");
        if (stripped.Length < 3) return false;

        return true;
    }

    private async Task<IActionResult> HandleAgentCommand(AgentCommand command)
    {
        var commandType = _parser.GetCommandType(command);
        _logger.LogInformation("Agent command: {CommandType} from {Player}", commandType, command.Player);

        // Block game-changing commands in read-only mode
        if (_readOnly && commandType is CommandType.Invocar or CommandType.Tiempo or CommandType.Clima)
        {
            var readOnlyMessage = "El narrador esta en modo solo lectura. No puedo ejecutar comandos que cambien el mundo.";
            await BroadcastMessageAsync($"[Agent] {readOnlyMessage}");
            return Ok(new { narration = readOnlyMessage, command = commandType.ToString() });
        }

        string narration;
        try
        {
            narration = commandType switch
            {
                CommandType.Narrar => await HandleNarrar(command),
                CommandType.Hora => await HandleHora(),
                CommandType.Clima => await HandleClima(command),
                CommandType.Tiempo => await HandleTiempo(command),
                CommandType.Invocar => await HandleInvocar(command),
                CommandType.Consejo => await HandleConsejo(),
                CommandType.Peligro => await HandlePeligro(),
                CommandType.Unknown when command.Command == "help" =>
                    "Comandos: /agente narrar|hora|clima|tiempo|invocar|consejo|peligro — o escribe libremente!",
                _ => "Comando no reconocido. Usa /agente [comando] o escribe libremente."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command {CommandType}", commandType);
            narration = "El narrador está temporalmente silencioso...";
        }

        await BroadcastMessageAsync($"[Agent] {narration}");

        // Return narration in response body for testing/API consumers
        return Ok(new { narration = narration, command = commandType.ToString() });
    }

    private async Task BroadcastMessageAsync(string message)
    {
        await _tshock.BroadcastMessageAsync(message);
    }

    private async Task<string> HandleNarrar(AgentCommand command)
    {
        var scene = string.Join(" ", command.Args);
        return await _groq.GenerateNarrationAsync(
            $"El jugador {command.Player} pide narrar: {scene}");
    }

    private async Task<string> HandleHora()
    {
        var status = await _tshock.GetStatusAsync();
        var context = "No se pudo obtener el estado del servidor.";
        if (status != null)
        {
            var timeOfDay = status.DayTime ? "de día" : "de noche";
            if (status.BloodMoon) timeOfDay += " con luna de sangre";
            if (status.Eclipse) timeOfDay += " con eclipse";
            context = $"El mundo está {timeOfDay}.";
        }
        return await _groq.GenerateNarrationAsync(
            $"¿Qué hora es en el mundo? El mundo está {context} Describe la hora actual de forma narrativa.",
            context);
    }

    private async Task<string> HandleClima(AgentCommand command)
    {
        var climate = command.Args.Length > 0 ? string.Join(" ", command.Args) : "normal";
        var tshockCmd = ClimateCommands.TryGetValue(climate, out var cmd)
            ? cmd
            : $"rain {climate}";

        await _tshock.ExecuteCommandAsync(tshockCmd);
        return await _groq.GenerateNarrationAsync(
            $"El clima cambia a {climate}. Narra el cambio de clima de forma dramática.");
    }

    private async Task<string> HandleTiempo(AgentCommand command)
    {
        var time = command.Args.Length > 0 ? command.Args[0] : "day";
        var tshockCmd = TimeCommands.TryGetValue(time, out var cmd)
            ? cmd
            : $"time {time}";

        await _tshock.ExecuteCommandAsync(tshockCmd);
        return await _groq.GenerateNarrationAsync(
            $"El tiempo cambia a {time}. Narra el cambio de hora de forma dramática.");
    }

    private async Task<string> HandleInvocar(AgentCommand command)
    {
        var boss = command.Args.Length > 0 ? string.Join(" ", command.Args) : "king slime";
        var tshockCmd = BossCommands.TryGetValue(boss, out var cmd)
            ? cmd
            : $"spawnboss {boss}";

        await _tshock.ExecuteCommandAsync(tshockCmd);
        return await _groq.GenerateNarrationAsync(
            $"¡El jugador {command.Player} ha invocado a {boss}! Narra la aparición del jefe de forma épica y dramática.");
    }

    private async Task<string> HandleConsejo()
    {
        return await _groq.GenerateNarrationAsync(
            "Da un consejo útil para jugar Terraria en dificultad Master. Sé conciso y dramático.");
    }

    private async Task<string> HandlePeligro()
    {
        return await _groq.GenerateNarrationAsync(
            "¡Advertencia de peligro! Narra una amenaza inminente de forma dramática.");
    }

    private static readonly HashSet<string> MechanicalCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "time", "worldevent", "hardmode", "save", "setspawn", "settle", "butcher", "maxspawns", "spawnrate"
    };

    private static bool IsMechanical(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return false;
        return MechanicalCommands.Contains(action.Trim().Split(' ')[0].TrimStart('/'));
    }

    private static bool IsGenericNarration(string? narration)
    {
        if (string.IsNullOrWhiteSpace(narration)) return true;
        var text = narration.Trim();
        if (text.Length < 40) return true;
        foreach (var prefix in new[] { "he hecho", "comando ejecutado", "el comando", "hecho:", "se ejecuta" })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
