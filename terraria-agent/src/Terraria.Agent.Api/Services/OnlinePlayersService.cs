namespace Terraria.Agent.Api.Services;

public sealed class OnlinePlayersSnapshot
{
    public bool IsReliable { get; init; }
    public List<string> Players { get; init; } = new();
}

public class OnlinePlayersService
{
    private readonly TShockClient _tshock;
    private readonly ILogger<OnlinePlayersService> _logger;
    private readonly TimeSpan _cacheDuration;

    private List<string>? _cache;
    private DateTime _cacheTime;
    private bool _cacheReliable;

    public OnlinePlayersService(TShockClient tshock, IConfiguration config, ILogger<OnlinePlayersService> logger)
    {
        _tshock = tshock;
        _logger = logger;
        _cacheDuration = TimeSpan.FromSeconds(Math.Max(1, config.GetValue("Agent:PlayersCacheSeconds", 5)));
    }

    public async Task<OnlinePlayersSnapshot> GetSnapshotAsync()
    {
        if (_cache != null && DateTime.UtcNow - _cacheTime < _cacheDuration)
            return new OnlinePlayersSnapshot { IsReliable = _cacheReliable, Players = _cache };

        try
        {
            var status = await _tshock.GetStatusAsync();
            var players = status?.Players
                .Select(p => p.Name.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            _cache = players;
            _cacheReliable = status != null;
            _cacheTime = DateTime.UtcNow;

            _logger.LogInformation("Online players refreshed: {Count} players ({Reliable})", players.Count, _cacheReliable);
            return new OnlinePlayersSnapshot { IsReliable = _cacheReliable, Players = players };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get online players; disabling guard until next refresh");
            _cache = new List<string>();
            _cacheReliable = false;
            _cacheTime = DateTime.UtcNow;
            return new OnlinePlayersSnapshot { IsReliable = false, Players = _cache };
        }
    }
}
