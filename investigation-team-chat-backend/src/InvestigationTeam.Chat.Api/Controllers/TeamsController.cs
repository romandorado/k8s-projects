using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InvestigationTeam.Chat.Api.Services;

namespace InvestigationTeam.Chat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly IInvestigationTeamProxy _proxy;

    public TeamsController(IInvestigationTeamProxy proxy) => _proxy = proxy;

    [HttpGet]
    public async Task<IActionResult> GetTeams()
    {
        var teams = await _proxy.GetTeamsAsync();
        return teams != null ? Ok(teams) : StatusCode(502);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTeam(Guid id)
    {
        var team = await _proxy.GetTeamAsync(id);
        return team != null ? Ok(team) : NotFound();
    }
}
