using Microsoft.AspNetCore.Mvc;
using Terraria.Agent.Api.Models;
using Terraria.Agent.Api.Services;

namespace Terraria.Agent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly CommandParser _parser;
    private readonly TShockClient _tshock;
    private readonly GroqService _groq;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        CommandParser parser, 
        TShockClient tshock, 
        GroqService groq,
        ILogger<ChatController> logger)
    {
        _parser = parser;
        _tshock = tshock;
        _groq = groq;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleEvent([FromBody] ChatEvent chatEvent)
    {
        _logger.LogInformation("Received event from {Player}: {Text}", chatEvent.Player, chatEvent.Text);

        var command = _parser.Parse(chatEvent);
        if (command == null)
            return Ok(); // Not an /agente command, ignore

        var commandType = _parser.GetCommandType(command);
        _logger.LogInformation("Parsed command: {CommandType} from {Player}", commandType, command.Player);

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
                _ => "Comando no reconocido. Usa /agente [narrar|hora|clima|tiempo|invocar|consejo|peligro]"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command {CommandType} from {Player}", commandType, command.Player);
            narration = "El narrador está temporalmente silencioso...";
        }

        await _tshock.BroadcastMessageAsync(narration);
        return Ok();
    }

    private async Task<string> HandleNarrar(AgentCommand command)
    {
        var scene = string.Join(" ", command.Args);
        return await _groq.GenerateNarrationAsync(
            $"El jugador {command.Player} pide narrar: {scene}");
    }

    private async Task<string> HandleHora()
    {
        var status = await _tshock.GetServerStatusAsync();
        var context = "No se pudo obtener el estado del servidor.";
        if (status != null)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(status);
                var root = doc.RootElement;
                var dayTime = root.TryGetProperty("dayTime", out var dt) && dt.GetBoolean();
                var bloodmoon = root.TryGetProperty("bloodmoon", out var bm) && bm.GetBoolean();
                var eclipse = root.TryGetProperty("eclipse", out var ec) && ec.GetBoolean();
                var timeOfDay = dayTime ? "de día" : "de noche";
                if (bloodmoon) timeOfDay += " con luna de sangre";
                if (eclipse) timeOfDay += " con eclipse";
                context = $"El mundo está {timeOfDay}.";
            }
            catch (System.Text.Json.JsonException)
            {
                context = "No se pudo parsear el estado del servidor.";
            }
        }
        return await _groq.GenerateNarrationAsync(
            $"¿Qué hora es en el mundo? El mundo está {context} Describe la hora actual de forma narrativa.",
            context);
    }

    private async Task<string> HandleClima(AgentCommand command)
    {
        var climate = command.Args.Length > 0 ? string.Join(" ", command.Args) : "normal";
        var tshockCmd = climate.ToLower() switch
        {
            "lluvia" => "rain 1",
            "nieve" => "rain 2",
            "tormenta" => "rain 3",
            "normal" => "rain 0",
            _ => $"rain {climate}"
        };
        
        await _tshock.ExecuteCommandAsync(tshockCmd);
        return await _groq.GenerateNarrationAsync(
            $"El clima cambia a {climate}. Narra el cambio de clima de forma dramática.");
    }

    private async Task<string> HandleTiempo(AgentCommand command)
    {
        var time = command.Args.Length > 0 ? command.Args[0] : "day";
        var tshockCmd = time.ToLower() switch
        {
            "dia" or "day" => "time day",
            "noche" or "night" => "time night",
            "mediodia" or "noon" => "time noon",
            "atardecer" or "dusk" => "time dusk",
            "medianoche" or "midnight" => "time midnight",
            _ => $"time {time}"
        };
        
        await _tshock.ExecuteCommandAsync(tshockCmd);
        return await _groq.GenerateNarrationAsync(
            $"El tiempo cambia a {time}. Narra el cambio de hora de forma dramática.");
    }

    private async Task<string> HandleInvocar(AgentCommand command)
    {
        var boss = command.Args.Length > 0 ? string.Join(" ", command.Args) : "king slime";
        var tshockCmd = boss.ToLower() switch
        {
            "wall of flesh" or "wall" or "muro" => "spawnboss WallOfFlesh",
            "king slime" or "slime" or "slim" => "spawnboss KingSlime",
            "eye of cthulhu" or "eye" or "ojo" => "spawnboss EyeOfCthulhu",
            "eater of worlds" or "eater" or "gusano" => "spawnboss EaterOfWorlds",
            "skeletron" or "esqueleto" => "spawnboss Skeletron",
            "queen bee" or "bee" or "abeja" => "spawnboss QueenBee",
            "twins" or "gemelos" => "spawnboss TheTwins",
            "destroyer" or "destructor" => "spawnboss TheDestroyer",
            "prime" or "skeletron prime" or "primo" => "spawnboss SkeletronPrime",
            "plantera" => "spawnboss Plantera",
            "golem" => "spawnboss Golem",
            "lunatic" or "lunatic cultist" or "cultista" => "spawnboss LunaticCultist",
            "moon lord" or "moon" or "lord" or "señor" => "spawnboss MoonLord",
            _ => $"spawnboss {boss}"
        };
        
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
