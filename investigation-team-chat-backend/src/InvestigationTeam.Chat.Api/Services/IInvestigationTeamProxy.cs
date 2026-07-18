using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Services;

public interface IInvestigationTeamProxy
{
    Task<List<AgentDto>?> GetAgentsAsync(string? bearerToken = null);
    Task<AgentDto?> GetAgentAsync(Guid id, string? bearerToken = null);
    Task<TeamDto?> GetTeamAsync(Guid id, string? bearerToken = null);
    Task<List<TeamDto>?> GetTeamsAsync(string? bearerToken = null);
}

public record AgentDto(Guid Id, string Name, int Role, string? Description, List<string> Skills, int Status);
public record TeamDto(Guid Id, string Name, string? Description, List<Guid> AgentIds);
