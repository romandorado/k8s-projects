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
            var url = $"{_baseUrl}/v3/server/rawcmd?cmd={Uri.EscapeDataString(command)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("TShockAuthorization", _token);
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("TShock command executed: {Command}, Response: {Response}", command, content);
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
        await ExecuteCommandAsync($"say [Agent] {message}");
    }

    public async Task<string?> GetServerStatusAsync()
    {
        try
        {
            var url = $"{_baseUrl}/v2/server/status";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("TShockAuthorization", _token);
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
