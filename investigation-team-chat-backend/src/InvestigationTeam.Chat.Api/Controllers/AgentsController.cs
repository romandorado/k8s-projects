using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InvestigationTeam.Chat.Api.Services;

namespace InvestigationTeam.Chat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly IInvestigationTeamProxy _proxy;

    public AgentsController(IInvestigationTeamProxy proxy) => _proxy = proxy;

    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        var agents = await _proxy.GetAgentsAsync();
        return agents != null ? Ok(agents) : StatusCode(502);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAgent(Guid id)
    {
        var agent = await _proxy.GetAgentAsync(id);
        return agent != null ? Ok(agent) : NotFound();
    }
}
