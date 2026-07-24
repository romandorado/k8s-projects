using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class TShockClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _token;
    private readonly string _pluginUrl;
    private readonly ILogger<TShockClient> _logger;

    private static readonly HashSet<string> InGameCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "spawnboss", "spawnmob", "time", "worldevent",
        "rain", "bloodmoon", "eclipse", "wind",
        "bridge", "butcher", "npc"
    };

    public TShockClient(HttpClient httpClient, IConfiguration config, ILogger<TShockClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = config["TShock:Url"]!;
        _token = config["TShock:Token"]!;
        _pluginUrl = config["TShock:PluginUrl"] ?? "http://terraria-server:7879";
        _logger = logger;
    }

    public async Task<string?> ExecuteCommandAsync(string command)
    {
        try
        {
            var cmd = command.StartsWith("/") ? command : $"/{command}";
            var baseCmd = cmd.Split(' ')[0].TrimStart('/');

            if (InGameCommands.Contains(baseCmd))
            {
                return await ExecuteViaPlugin(cmd);
            }

            var url = $"{_baseUrl}/v3/server/rawcmd?cmd={Uri.EscapeDataString(cmd)}&token={Uri.EscapeDataString(_token)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("TShock command executed: {Command}, Response: {Response}", cmd, content);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute TShock command: {Command}", command);
            return null;
        }
    }

    private async Task<string?> ExecuteViaPlugin(string command)
    {
        try
        {
            var payload = new { command };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var url = $"{_pluginUrl}/execute";
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Plugin command executed: {Command}, Response: {Response}", command, content);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute plugin command: {Command}", command);
            return null;
        }
    }

    public async Task BroadcastMessageAsync(string message)
    {
        try
        {
            var url = $"{_baseUrl}/v2/server/broadcast?msg={Uri.EscapeDataString(message)}&token={Uri.EscapeDataString(_token)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Broadcast sent: {Message}, Response: {Response}", message, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast message: {Message}", message);
        }
    }

    public async Task<string?> GetServerStatusAsync()
    {
        try
        {
            var url = $"{_baseUrl}/v2/server/status?token={Uri.EscapeDataString(_token)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get server status");
            return null;
        }
    }

    public async Task<TerrariaStatus?> GetStatusAsync()
    {
        try
        {
            var url = $"{_baseUrl}/v2/server/status?token={Uri.EscapeDataString(_token)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var status = new TerrariaStatus
            {
                DayTime = root.TryGetProperty("dayTime", out var dt) && dt.GetBoolean(),
                BloodMoon = root.TryGetProperty("bloodmoon", out var bm) && bm.GetBoolean(),
                Eclipse = root.TryGetProperty("eclipse", out var ec) && ec.GetBoolean()
            };

            if (root.TryGetProperty("players", out var playersArr))
            {
                foreach (var p in playersArr.EnumerateArray())
                {
                    status.Players.Add(new PlayerInfo
                    {
                        Name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Id = p.TryGetProperty("id", out var id) ? id.GetInt32() : 0
                    });
                }
            }

            _logger.LogInformation("Server status: dayTime={DayTime}, blood={Blood}, eclipse={Eclipse}, players={Count}",
                status.DayTime, status.BloodMoon, status.Eclipse, status.Players.Count);

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get server status");
            return null;
        }
    }

    public async Task<string?> SpawnBossAsync(string boss)
    {
        var cmd = $"/spawnboss {boss}";
        _logger.LogInformation("Spawning boss: {Boss}", boss);
        return await ExecuteViaPlugin(cmd);
    }
}
