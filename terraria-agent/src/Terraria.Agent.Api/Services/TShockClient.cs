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
        "spawnboss", "spawnmob"
    };

    private static readonly HashSet<string> BridgeCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "bridge rain off", "bridge rain stop", "bridge rain clear",
        "bridge rain on", "bridge rain start", "bridge rain heavy", "bridge rain max", "bridge rain",
        "bridge wind",
        "bridge bloodmoon on", "bridge bloodmoon off", "bridge bloodmoon stop", "bridge bloodmoon",
        "bridge eclipse on", "bridge eclipse off", "bridge eclipse stop", "bridge eclipse"
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

            if (InGameCommands.Contains(baseCmd) || baseCmd.Equals("bridge", StringComparison.OrdinalIgnoreCase))
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
}
