using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvestigationTeam.Api.Data;
using InvestigationTeam.Api.Models;

namespace InvestigationTeam.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TeamsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Team>>> GetTeams()
    {
        return await _context.Teams.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Team>> GetTeam(Guid id)
    {
        var team = await _context.Teams.FindAsync(id);
        if (team == null)
            return NotFound();

        return team;
    }

    [HttpPost]
    public async Task<ActionResult<Team>> CreateTeam(CreateTeamRequest request)
    {
        var team = new Team
        {
            Name = request.Name,
            Description = request.Description
        };

        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTeam), new { id = team.Id }, team);
    }

    [HttpPost("{id}/agents/{agentId}")]
    public async Task<IActionResult> AddAgentToTeam(Guid id, Guid agentId)
    {
        var team = await _context.Teams.FindAsync(id);
        if (team == null)
            return NotFound("Equipo no encontrado");

        var agent = await _context.Agents.FindAsync(agentId);
        if (agent == null)
            return NotFound("Agente no encontrado");

        if (!team.AgentIds.Contains(agentId))
        {
            team.AgentIds.Add(agentId);
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Agente agregado al equipo" });
    }

    [HttpDelete("{id}/agents/{agentId}")]
    public async Task<IActionResult> RemoveAgentFromTeam(Guid id, Guid agentId)
    {
        var team = await _context.Teams.FindAsync(id);
        if (team == null)
            return NotFound();

        team.AgentIds.Remove(agentId);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Agente removido del equipo" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTeam(Guid id)
    {
        var team = await _context.Teams.FindAsync(id);
        if (team == null)
            return NotFound();

        _context.Teams.Remove(team);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
