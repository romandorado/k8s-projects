using System.Text.Json;

namespace Terraria.Agent.Api.Services;

public class CraftingService
{
    private readonly ILogger<CraftingService> _logger;
    private readonly WikiService _wiki;
    private readonly Dictionary<string, JsonElement> _data = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    public CraftingService(ILogger<CraftingService> logger, WikiService wiki)
    {
        _logger = logger;
        _wiki = wiki;
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "crafting.json");
            if (!File.Exists(path))
            {
                _logger.LogWarning("crafting.json not found at {Path}", path);
                return;
            }

            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            foreach (var category in root.EnumerateObject())
            {
                if (category.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var item in category.Value.EnumerateObject())
                    {
                        _data[item.Name] = item.Value;
                        if (item.Value.TryGetProperty("name", out var nameProp))
                        {
                            _aliases[nameProp.GetString() ?? item.Name] = item.Name;
                        }
                    }
                }
            }

            _logger.LogInformation("Loaded {Count} crafting items", _data.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load crafting data");
        }
    }

    public async Task<string?> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        query = query.Trim().ToLowerInvariant();

        // Try local DB first
        if (_data.TryGetValue(query, out var directMatch))
            return FormatItem(directMatch);

        if (_aliases.TryGetValue(query, out var aliasKey) && _data.TryGetValue(aliasKey, out var aliasMatch))
            return FormatItem(aliasMatch);

        foreach (var (key, element) in _data)
        {
            if (element.TryGetProperty("name", out var nameProp))
            {
                var name = nameProp.GetString() ?? "";
                if (name.ToLowerInvariant().Contains(query) || query.Contains(name.ToLowerInvariant()))
                    return FormatItem(element);
            }
        }

        foreach (var (key, element) in _data)
        {
            if (key.Contains(query) || element.ToString().ToLowerInvariant().Contains(query))
                return FormatItem(element);
        }

        // Fallback: search Terraria Wiki (persisted to disk)
        var wikiResult = await _wiki.SearchRecipe(query);
        if (wikiResult != null)
            return wikiResult;

        return null;
    }

    private string FormatItem(JsonElement element)
    {
        var lines = new List<string>();

        if (element.TryGetProperty("name", out var name))
            lines.Add($"**{name.GetString()}**");

        if (element.TryGetProperty("recipe", out var recipe) && recipe.ValueKind == JsonValueKind.Object)
        {
            var materials = new List<string>();
            foreach (var mat in recipe.EnumerateObject())
            {
                var count = mat.Value.ValueKind == JsonValueKind.Number ? mat.Value.GetInt32() : 1;
                materials.Add($"{count}x {mat.Name.Replace('_', ' ')}");
            }
            lines.Add($"Receta: {string.Join(", ", materials)}");
        }

        if (element.TryGetProperty("station", out var station))
            lines.Add($"Estación: {station.GetString()}");

        if (element.TryGetProperty("drop", out var drop))
            lines.Add($"Drop: {drop.GetString()}");

        if (element.TryGetProperty("conditions", out var conditions))
            lines.Add($"Condiciones: {conditions.GetString()}");

        if (element.TryGetProperty("summon", out var summon))
            lines.Add($"Invocación: {summon.GetString()}");

        if (element.TryGetProperty("recipe", out var recipe2) && recipe2.ValueKind == JsonValueKind.Array)
        {
            var materials = new List<string>();
            foreach (var mat in recipe2.EnumerateArray())
            {
                if (mat.ValueKind == JsonValueKind.Object)
                {
                    foreach (var m in mat.EnumerateObject())
                    {
                        var count = m.Value.ValueKind == JsonValueKind.Number ? m.Value.GetInt32() : 1;
                        materials.Add($"{count}x {m.Name.Replace('_', ' ')}");
                    }
                }
            }
            if (materials.Count > 0)
                lines.Add($"Receta: {string.Join(", ", materials)}");
        }

        if (element.TryGetProperty("pieces", out var pieces) && pieces.ValueKind == JsonValueKind.Object)
        {
            foreach (var piece in pieces.EnumerateObject())
            {
                if (piece.Value.TryGetProperty("recipe", out var pieceRecipe))
                {
                    var materials = new List<string>();
                    foreach (var mat in pieceRecipe.EnumerateObject())
                    {
                        var count = mat.Value.ValueKind == JsonValueKind.Number ? mat.Value.GetInt32() : 1;
                        materials.Add($"{count}x {mat.Name.Replace('_', ' ')}");
                    }
                    lines.Add($"{piece.Name}: {string.Join(", ", materials)}");
                }
            }
        }

        if (element.TryGetProperty("drops", out var drops) && drops.ValueKind == JsonValueKind.Array)
        {
            var dropList = new List<string>();
            foreach (var dropItem in drops.EnumerateArray())
                dropList.Add(dropItem.GetString() ?? "");
            lines.Add($"Drops: {string.Join(", ", dropList)}");
        }

        return string.Join("\n", lines);
    }

    public List<string> SearchMultiple(string query, int maxResults = 3)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        query = query.Trim().ToLowerInvariant();
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, element) in _data)
        {
            if (results.Count >= maxResults) break;

            if (element.TryGetProperty("name", out var nameProp))
            {
                var name = nameProp.GetString() ?? "";
                if ((name.ToLowerInvariant().Contains(query) || query.Contains(name.ToLowerInvariant())) && found.Add(name))
                    results.Add(FormatItem(element));
            }
        }

        return results;
    }
}
