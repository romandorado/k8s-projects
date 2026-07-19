using System.Net.Http.Json;

namespace Terraria.Agent.Api.Services;

public class AutoEventService : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<AutoEventService> _logger;
    private readonly Random _random = new();

    private static readonly string[] Events = new[]
    {
        "[EVENTO] El sol se oculta lentamente. Narra el atardecer de forma poética.",
        "[EVENTO] Un grupo de slimes se acerca al pueblo. Narra la amenaza.",
        "[EVENTO] El viento sopla fuerte. Narra el cambio de clima.",
        "[EVENTO] Se escuchan ruidos extraños bajo tierra. Narra la tensión.",
        "[EVENTO] Un arcoíris aparece en el cielo. Narra la belleza del momento.",
        "[EVENTO] Los pájaros cantan en el bosque. Narra la paz del mundo.",
        "[EVENTO] Una estrella fugaz cruza el cielo. Narra el momento mágico.",
        "[EVENTO] El mar está calmado. Narra la tranquilidad antes de la tormenta.",
        "[EVENTO] Un goblin fue visto cerca del pueblo. Narra la alerta.",
        "[EVENTO] Las luciérnagas salen a bailar. Narra la magia de la noche."
    };

    public AutoEventService(HttpClient httpClient, IConfiguration config, ILogger<AutoEventService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoEventService started. Events every 10 minutes.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait 10 minutes between events
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

                var agentUrl = _config["TShock:PluginUrl"] ?? "http://terraria-server:7879";
                var token = _config["Agent:Token"] ?? "terraria-agent-secret-token-2024";

                // Pick random event
                var eventText = Events[_random.Next(Events.Length)];

                var payload = new
                {
                    Player = "Sistema",
                    Text = eventText,
                    Event = "auto"
                };

                _logger.LogInformation("AutoEvent: {Event}", eventText);

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_config["TShock:Url"] ?? "http://terraria-server:7878"}/v2/server/broadcast")
                {
                    Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(new { Text = $"[Narrador] {eventText}" }),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
                request.Headers.Add("Token", _config["TShock:Token"] ?? "terraria-agent-secret-token-2024");

                // Actually, let's send to the agent instead
                var agentRequest = new HttpRequestMessage(HttpMethod.Post, "http://terraria-agent:8080/api/chat")
                {
                    Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
                agentRequest.Headers.Add("X-Agent-Token", token);

                await _httpClient.SendAsync(agentRequest, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoEvent error");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
