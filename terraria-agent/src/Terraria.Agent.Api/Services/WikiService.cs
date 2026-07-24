using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Terraria.Agent.Api.Services;

public class WikiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WikiService> _logger;
    private readonly string _cachePath;
    private Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    private const string WikiBaseUrl = "https://terraria.wiki.gg";
    private const string SearchApi = "https://terraria.wiki.gg/api.php";

    public WikiService(HttpClient httpClient, ILogger<WikiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TerrariaAgent/1.0 (Educational)");
        _cachePath = Path.Combine(AppContext.BaseDirectory, "Data", "wiki-cache.json");
        LoadCache();
    }

    private void LoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                var json = File.ReadAllText(_cachePath);
                _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                         ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _logger.LogInformation("Loaded {Count} cached wiki entries", _cache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load wiki cache");
            _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveCache()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cachePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cachePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save wiki cache");
        }
    }

    public async Task<string?> SearchRecipe(string query)
    {
        // Check cache first
        if (_cache.TryGetValue(query, out var cached))
            return cached;

        try
        {
            var result = await SearchWiki(query);
            if (result != null)
            {
                _cache[query] = result;
                SaveCache();
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wiki search failed for {Query}", query);
        }

        return null;
    }

    private async Task<string?> SearchWiki(string query)
    {
        var searchUrl = $"{SearchApi}?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&format=json&srlimit=3";
        var searchResponse = await _httpClient.GetStringAsync(searchUrl);

        using var searchDoc = JsonDocument.Parse(searchResponse);
        var results = searchDoc.RootElement.GetProperty("query").GetProperty("search");

        if (results.GetArrayLength() == 0)
            return null;

        var pageTitle = results[0].GetProperty("title").GetString() ?? query;

        var pageUrl = $"{SearchApi}?action=parse&page={Uri.EscapeDataString(pageTitle)}&prop=text&format=json";
        var pageResponse = await _httpClient.GetStringAsync(pageUrl);

        using var pageDoc = JsonDocument.Parse(pageResponse);
        var html = pageDoc.RootElement.GetProperty("parse").GetProperty("text").GetProperty("*").GetString() ?? "";

        return ParseRecipeFromHtml(pageTitle, html);
    }

    private string? ParseRecipeFromHtml(string title, string html)
    {
        var lines = new List<string> { $"**{title}**" };

        var recipeTableMatch = Regex.Match(html, @"<table[^>]*class=""terraria[^""]*recipes[^""]*""[^>]*>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (recipeTableMatch.Success)
        {
            var table = recipeTableMatch.Groups[1].Value;
            var rowMatches = Regex.Matches(table, @"<tr\s+data-rowid=""?\d+""?>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match rowMatch in rowMatches)
            {
                var row = rowMatch.Groups[1].Value;

                var ingredientMatch = Regex.Match(row, @"<td\s+class=""ingredients"">(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (!ingredientMatch.Success) continue;

                var ingredientsHtml = ingredientMatch.Groups[1].Value;
                var items = Regex.Matches(ingredientsHtml, @"<a\s+href=""[^""]*""\s+title=""([^""]+)"">", RegexOptions.IgnoreCase);
                var ingredients = items.Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Where(n => n != title && !n.Contains("Crafting station"))
                    .Distinct()
                    .ToList();

                var stationMatch = Regex.Match(row, @"<td\s+class=""station"">(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var station = "";
                if (stationMatch.Success)
                {
                    var stationName = Regex.Match(stationMatch.Groups[1].Value, @"title=""([^""]+)""");
                    if (stationName.Success)
                        station = stationName.Groups[1].Value;
                }

                if (ingredients.Count > 0)
                {
                    lines.Add($"Receta: {string.Join(", ", ingredients)}");
                    if (!string.IsNullOrEmpty(station))
                        lines.Add($"Estación: {station}");
                }
            }
        }

        var descMatch = Regex.Match(html, @"<div[^>]*id=""mw-content-text""[^>]*>.*?<p>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (descMatch.Success)
        {
            var desc = Regex.Replace(descMatch.Groups[1].Value, @"<[^>]+>", "").Trim();
            if (desc.Length > 30)
                lines.Add(desc.Length > 200 ? desc[..200] + "..." : desc);
        }

        return lines.Count > 1 ? string.Join("\n", lines) : null;
    }
}
