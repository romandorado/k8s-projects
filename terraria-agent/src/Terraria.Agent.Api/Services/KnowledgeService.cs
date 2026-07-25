using System.Text.Json;

namespace Terraria.Agent.Api.Services;

public class KnowledgeService
{
    private readonly ILogger<KnowledgeService> _logger;
    private readonly CraftingService _crafting;
    private readonly string _dataPath;
    
    private List<BossData> _bosses = new();
    private List<GameData> _gameData = new();

    public KnowledgeService(IConfiguration config, ILogger<KnowledgeService> logger, CraftingService crafting)
    {
        _logger = logger;
        _crafting = crafting;
        _dataPath = config["Data:Path"] ?? "Data";
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            var bossesPath = Path.Combine(_dataPath, "bosses.json");
            if (File.Exists(bossesPath))
            {
                var json = File.ReadAllText(bossesPath);
                _bosses = JsonSerializer.Deserialize<List<BossData>>(json) ?? new();
                _logger.LogInformation("Loaded {Count} boss entries", _bosses.Count);
            }

            var gamePath = Path.Combine(_dataPath, "gamedata.json");
            if (File.Exists(gamePath))
            {
                var json = File.ReadAllText(gamePath);
                _gameData = JsonSerializer.Deserialize<List<GameData>>(json) ?? new();
                _logger.LogInformation("Loaded {Count} game data entries", _gameData.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load knowledge data");
        }
    }

    public async Task<string?> SearchCraftingAsync(string query)
    {
        return await _crafting.Search(query);
    }

    public string? SearchBoss(string query)
    {
        var lower = query.ToLower();
        var match = _bosses.FirstOrDefault(b => 
            lower.Contains(b.Name.ToLower()) ||
            b.Aliases.Any(a => lower.Contains(a.ToLower())));
        
        return match?.ToContextString();
    }

    public string GetGameContext()
    {
        return "Mundo: MundoSobrinos (Master difficulty). Jugadores: verificar en vivo.";
    }

    public string GetKnowledgeContext(string query)
    {
        var context = new List<string>();
        
        // Search crafting
        var craftingResult = _crafting.Search(query).Result;
        if (!string.IsNullOrEmpty(craftingResult))
            context.Add($"CRAFTING:\n{craftingResult}");
        
        // Search boss
        var bossResult = SearchBoss(query);
        if (!string.IsNullOrEmpty(bossResult))
            context.Add($"BOSS:\n{bossResult}");
        
        // Search game data
        var lower = query.ToLower();
        var gameMatches = _gameData.Where(g => 
            lower.Contains(g.Topic.ToLower()) ||
            g.Keywords.Any(k => lower.Contains(k.ToLower())));
        foreach (var match in gameMatches.Take(2))
            context.Add($"{match.Topic.ToUpper()}:\n{match.Info}");
        
        return context.Count > 0 
            ? string.Join("\n\n", context) 
            : "";
    }
}

public class BossData
{
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public int Hp { get; set; }
    public int Damage { get; set; }
    public int Defense { get; set; }
    public string[] Drops { get; set; } = Array.Empty<string>();
    public string SpawnCondition { get; set; } = "";
    public string Tips { get; set; } = "";

    public string ToContextString()
    {
        return $"Boss: {Name}\nHP: {Hp}\nDaño: {Damage}\nDefensa: {Defense}\n" +
               $"Drops: {string.Join(", ", Drops)}\nInvocación: {SpawnCondition}\nTips: {Tips}";
    }
}

public class GameData
{
    public string Topic { get; set; } = "";
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string Info { get; set; } = "";
}
