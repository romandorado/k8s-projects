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
        ["señor"] = "spawnboss MoonLord"
    };

    public ChatController(
        CommandParser parser,
        TShockClient tshock,
        GroqService groq,
        IntentParser intentParser,
        ILogger<ChatController> logger,
        IConfiguration config)
    {
        _parser = parser;
        _tshock = tshock;
        _groq = groq;
        _intentParser = intentParser;
        _logger = logger;
        _config = config;
        _readOnly = config.GetValue<bool>("Agent:ReadOnly", false);
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

        // Route 2: Natural language (IntentParser via Groq)
        var intent = await _intentParser.ParseAsync(chatEvent);
        if (intent == null || string.IsNullOrWhiteSpace(intent.Narration) || !intent.Respond)
        {
            _logger.LogInformation("IntentParser: no response for {Player} (respond={Respond})", chatEvent.Player, intent?.Respond);
            return Ok();
        }

        // Execute TShock action if detected
        if (!string.IsNullOrWhiteSpace(intent.Action))
        {
            if (_readOnly)
            {
                _logger.LogInformation("Read-only mode: skipping action {Action}", intent.Action);
            }
            else
            {
                _logger.LogInformation("Executing action: {Action}", intent.Action);
                await _tshock.ExecuteCommandAsync(intent.Action);
            }
        }

        // Broadcast narration
        await BroadcastMessageAsync($"[Narrador] {intent.Narration}");

        // Return narration in response body for testing/API consumers
        return Ok(new { narration = intent.Narration, action = intent.Action });
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
}
