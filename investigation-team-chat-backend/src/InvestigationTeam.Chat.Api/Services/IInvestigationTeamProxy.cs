using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Services;

public interface IInvestigationTeamProxy
{
    Task<List<AgentDto>?> GetAgentsAsync();
    Task<AgentDto?> GetAgentAsync(Guid id);
    Task<TeamDto?> GetTeamAsync(Guid id);
    Task<List<TeamDto>?> GetTeamsAsync();
}

public record AgentDto(Guid Id, string Name, int Role, string? Description, List<string> Skills, int Status);
public record TeamDto(Guid Id, string Name, string? Description, List<Guid> AgentIds);
