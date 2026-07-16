using System.Text.Json;
using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Services;

public class InvestigationTeamProxy : IInvestigationTeamProxy
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public InvestigationTeamProxy(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["InvestigationTeamApi:BaseUrl"]!;
    }

    public async Task<List<AgentDto>?> GetAgentsAsync()
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/Agents");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<AgentDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<AgentDto?> GetAgentAsync(Guid id)
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/Agents/{id}");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AgentDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<TeamDto?> GetTeamAsync(Guid id)
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/Teams/{id}");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TeamDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<List<TeamDto>?> GetTeamsAsync()
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/Teams");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<TeamDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
