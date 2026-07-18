namespace Terraria.Agent.Api.Services;

public class TShockClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _token;
    private readonly ILogger<TShockClient> _logger;

    public TShockClient(HttpClient httpClient, IConfiguration config, ILogger<TShockClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = config["TShock:Url"]!;
        _token = config["TShock:Token"]!;
        _logger = logger;
    }

    public async Task<string?> ExecuteCommandAsync(string command)
    {
        try
        {
            var cmd = command.StartsWith("/") ? command : $"/{command}";
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

    public async Task BroadcastMessageAsync(string message)
    {
        try
        {
            var url = $"{_baseUrl}/v2/server/broadcast?msg={Uri.EscapeDataString($"[Agent] {message}")}&token={Uri.EscapeDataString(_token)}";
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
