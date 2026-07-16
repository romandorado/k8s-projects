namespace InvestigationTeam.Api.Models;

public enum AgentRole
{
    Researcher,
    Analyst,
    Writer,
    Coordinator,
    Reviewer
}

public enum AgentStatus
{
    Active,
    Inactive,
    Busy
}

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public string? Description { get; set; }
    public List<string> Skills { get; set; } = new();
    public AgentStatus Status { get; set; } = AgentStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Guid> AgentIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CreateAgentRequest
{
    public string Name { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public string? Description { get; set; }
    public List<string> Skills { get; set; } = new();
}

public class UpdateAgentRequest
{
    public string? Name { get; set; }
    public AgentRole? Role { get; set; }
    public string? Description { get; set; }
    public List<string>? Skills { get; set; }
    public AgentStatus? Status { get; set; }
}

public class CreateTeamRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
