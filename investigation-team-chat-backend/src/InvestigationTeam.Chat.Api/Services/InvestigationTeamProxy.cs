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

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string? bearerToken)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(bearerToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        return request;
    }

    public async Task<List<AgentDto>?> GetAgentsAsync(string? bearerToken = null)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, $"{_baseUrl}/api/Agents", bearerToken);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<AgentDto>>(json, JsonOptions) ?? new();
        }
        catch { return null; }
    }

    public async Task<AgentDto?> GetAgentAsync(Guid id, string? bearerToken = null)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, $"{_baseUrl}/api/Agents/{id}", bearerToken);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AgentDto>(json, JsonOptions);
        }
        catch { return null; }
    }

    public async Task<TeamDto?> GetTeamAsync(Guid id, string? bearerToken = null)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, $"{_baseUrl}/api/Teams/{id}", bearerToken);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TeamDto>(json, JsonOptions);
        }
        catch { return null; }
    }

    public async Task<List<TeamDto>?> GetTeamsAsync(string? bearerToken = null)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, $"{_baseUrl}/api/Teams", bearerToken);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<TeamDto>>(json, JsonOptions) ?? new();
        }
        catch { return null; }
    }
}
