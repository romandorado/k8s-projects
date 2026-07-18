using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvestigationTeam.Api.Data;
using InvestigationTeam.Api.Models;

namespace InvestigationTeam.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AgentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Agent>>> GetAgents()
    {
        return await _context.Agents.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Agent>> GetAgent(Guid id)
    {
        var agent = await _context.Agents.FindAsync(id);
        if (agent == null)
            return NotFound();

        return agent;
    }

    [HttpPost]
    public async Task<ActionResult<Agent>> CreateAgent(CreateAgentRequest request)
    {
        var agent = new Agent
        {
            Name = request.Name,
            Role = request.Role,
            Description = request.Description,
            Skills = request.Skills
        };

        _context.Agents.Add(agent);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "Error creating agent" });
        }

        return CreatedAtAction(nameof(GetAgent), new { id = agent.Id }, agent);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAgent(Guid id, UpdateAgentRequest request)
    {
        var agent = await _context.Agents.FindAsync(id);
        if (agent == null)
            return NotFound();

        if (request.Name != null) agent.Name = request.Name;
        if (request.Role.HasValue) agent.Role = request.Role.Value;
        if (request.Description != null) agent.Description = request.Description;
        if (request.Skills != null) agent.Skills = request.Skills;
        if (request.Status.HasValue) agent.Status = request.Status.Value;
        agent.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "Error updating agent" });
        }
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAgent(Guid id)
    {
        var agent = await _context.Agents.FindAsync(id);
        if (agent == null)
            return NotFound();

        _context.Agents.Remove(agent);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "Error deleting agent" });
        }

        return NoContent();
    }
}
