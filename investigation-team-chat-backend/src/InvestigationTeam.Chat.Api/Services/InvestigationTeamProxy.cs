using System.Text.Json;
using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Services;

public class InvestigationTeamProxy : IInvestigationTeamProxy
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public InvestigationTeamProxy(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["InvestigationTeamApi:BaseUrl"] ?? "http://investigation-team-api.investigation-team.svc.cluster.local";
    }

    public async Task<List<AgentDto>?> GetAgentsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/Agents");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<AgentDto>>(json, JsonOptions) ?? new();
        }
        catch { return null; }
    }

    public async Task<AgentDto?> GetAgentAsync(Guid id)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/Agents/{id}");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AgentDto>(json, JsonOptions);
        }
        catch { return null; }
    }

    public async Task<TeamDto?> GetTeamAsync(Guid id)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/Teams/{id}");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TeamDto>(json, JsonOptions);
        }
        catch { return null; }
    }

    public async Task<List<TeamDto>?> GetTeamsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/Teams");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<TeamDto>>(json, JsonOptions) ?? new();
        }
        catch { return null; }
    }
}
