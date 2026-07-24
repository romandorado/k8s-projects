using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class AutoEventService : BackgroundService
{
    private readonly TShockClient _tshock;
    private readonly GroqService _groq;
    private readonly AutoEventConfig _config;
    private readonly ILogger<AutoEventService> _logger;
    private readonly Random _random = new();

    private bool? _lastDayTime;
    private bool? _lastBloodMoon;
    private bool? _lastEclipse;

    private DateTime _nextAmbientTime;
    private DateTime _nextBossCheckTime;

    private static readonly string[] AmbientScenarios = new[]
    {
        "Un viento suave mueve las hojas del bosque cercano. Narra la brisa.",
        "Se escuchan grillos en la distancia. Narra la tranquilidad nocturna.",
        "Un rayo de sol atraviesa las nubes. Narra la luz dorada.",
        "Las luciérnagas bailan cerca del agua. Narra la magia del momento.",
        "Un murciélago pasa volando. Narra la presencia inesperada.",
        "El mar hace eco contra las rocas. Narra la calma del océano.",
        "Una hoja cae lentamente del árbol. Narra el cambio de estación.",
        "Se escucha un aullido lejano. Narra la misteriosa criatura.",
        "Las estrellas brillan con fuerza. Narra la inmensidad del cielo.",
        "Un cohete estrella cruza el cielo. Narra el momento efímero."
    };

    public AutoEventService(
        TShockClient tshock,
        GroqService groq,
        IConfiguration config,
        ILogger<AutoEventService> logger)
    {
        _tshock = tshock;
        _groq = groq;
        _logger = logger;

        _config = new AutoEventConfig();
        config.GetSection("AutoEvent").Bind(_config);

        _nextAmbientTime = DateTime.UtcNow.AddMinutes(_config.AmbientIntervalMinutes);
        _nextBossCheckTime = DateTime.UtcNow.AddMinutes(_config.BossCheckIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AutoEventService started. Poll={Poll}s, Ambient={Ambient}min, Boss={Boss}min",
            _config.PollIntervalSeconds, _config.AmbientIntervalMinutes, _config.BossCheckIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds), stoppingToken);

                var status = await _tshock.GetStatusAsync();
                if (status == null)
                {
                    _logger.LogWarning("Could not get server status, skipping tick");
                    continue;
                }

                var playerCount = status.Players.Count;

                // Always track state changes (even with 0 players)
                DetectTransitions(status);

                // Skip Groq calls if no players
                if (playerCount == 0)
                {
                    _logger.LogDebug("No players online, skipping events");
                    continue;
                }

                // Handle day/night transitions
                if (_lastDayTime.HasValue && _lastDayTime != status.DayTime)
                {
                    await NarrateTransition(status, status.DayTime
                        ? "El amanecer llega al mundo. Narra el inicio de un nuevo día de forma épica."
                        : "La noche cae sobre el mundo. Narra la llegada de la oscuridad de forma dramática.");
                }

                // Handle bloodmoon
                if (_lastBloodMoon.HasValue && _lastBloodMoon != status.BloodMoon)
                {
                    if (status.BloodMoon)
                        await NarrateTransition(status, "¡Una LUNA DE SANGRE aparece! Narra el evento aterrador.");
                    else
                        await NarrateTransition(status, "La luna de sangre se disipa. Narra el alivio del mundo.");
                }

                // Handle eclipse
                if (_lastEclipse.HasValue && _lastEclipse != status.Eclipse)
                {
                    if (status.Eclipse)
                        await NarrateTransition(status, "¡Un ECLIPSE oscurece el cielo! Narra la llegada de las sombras.");
                    else
                        await NarrateTransition(status, "El eclipse termina. Narra la luz retornando al mundo.");
                }

                // Ambient events
                if (DateTime.UtcNow >= _nextAmbientTime)
                {
                    await FireAmbientEvent(status);
                    _nextAmbientTime = DateTime.UtcNow.AddMinutes(
                        _config.AmbientIntervalMinutes + _random.Next(-_config.AmbientJitterMinutes, _config.AmbientJitterMinutes + 1));
                }

                // Boss check
                if (DateTime.UtcNow >= _nextBossCheckTime && playerCount >= _config.MinPlayersForBoss)
                {
                    await DecideBoss(status);
                    _nextBossCheckTime = DateTime.UtcNow.AddMinutes(
                        _config.BossCheckIntervalMinutes + _random.Next(-_config.BossCheckJitterMinutes, _config.BossCheckJitterMinutes + 1));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoEvent tick error");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private void DetectTransitions(TerrariaStatus status)
    {
        _lastDayTime = status.DayTime;
        _lastBloodMoon = status.BloodMoon;
        _lastEclipse = status.Eclipse;
    }

    private async Task NarrateTransition(TerrariaStatus status, string prompt)
    {
        try
        {
            _logger.LogInformation("Transition detected, narrating...");
            var narration = await _groq.GenerateEventNarrationAsync(prompt, status);
            await _tshock.BroadcastMessageAsync($"[Narrador] {narration}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to narrate transition");
        }
    }

    private async Task FireAmbientEvent(TerrariaStatus status)
    {
        try
        {
            var scenario = AmbientScenarios[_random.Next(AmbientScenarios.Length)];
            _logger.LogInformation("Ambient event: {Scenario}", scenario);
            var narration = await _groq.GenerateEventNarrationAsync(scenario, status);
            await _tshock.BroadcastMessageAsync($"[Narrador] {narration}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fire ambient event");
        }
    }

    private async Task DecideBoss(TerrariaStatus status)
    {
        try
        {
            var context = $"Hora: {(status.DayTime ? "día" : "noche")}. " +
                          $"Eventos: {(status.BloodMoon ? "luna de sangre " : "")}{(status.Eclipse ? "eclipse " : "")}. " +
                          $"Jugadores: {string.Join(", ", status.Players.Select(p => p.Name))}.";

            var prompt = $@"Contexto del mundo: {context}
Dificultad: Master.

¿Debería invocar un boss ahora? Considera la hora, los eventos activos y los jugadores.
Responde SOLO con este JSON:
{{""spawn"": true/false, ""boss"": ""nombre del boss o null"", ""reason"": ""por qué""}}

Bosses disponibles: KingSlime, EyeOfCthulhu, EaterOfWorlds, Skeletron, QueenBee, TheTwins, TheDestroyer, SkeletronPrime, Plantera, Golem, LunaticCultist, MoonLord";

            var narration = await _groq.GenerateEventNarrationAsync(prompt, status);

            // Parse the response
            var json = narration.Trim();
            if (json.StartsWith("```"))
                json = json.Replace("```json", "").Replace("```", "").Trim();

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var shouldSpawn = root.TryGetProperty("spawn", out var s) && s.GetBoolean();
            var boss = root.TryGetProperty("boss", out var b) ? b.GetString() : null;
            var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : "";

            if (shouldSpawn && !string.IsNullOrEmpty(boss))
            {
                _logger.LogInformation("Boss decision: SPAWN {Boss} — {Reason}", boss, reason);

                // Generate epic narration before spawning
                var epicNarration = await _groq.GenerateEventNarrationAsync(
                    $"¡Un {boss} aparece en el mundo! {reason} Narra la aparición de forma épica y dramática.",
                    status);

                await _tshock.SpawnBossAsync(boss);
                await _tshock.BroadcastMessageAsync($"[Narrador] {epicNarration}");
            }
            else
            {
                _logger.LogInformation("Boss decision: NO SPAWN — {Reason}", reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decide boss");
        }
    }
}
