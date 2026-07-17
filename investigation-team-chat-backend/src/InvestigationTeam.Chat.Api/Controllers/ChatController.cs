using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvestigationTeam.Chat.Api.Data;
using InvestigationTeam.Chat.Api.Models;
using InvestigationTeam.Chat.Api.Services;

namespace InvestigationTeam.Chat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ChatDbContext _context;
    private readonly IGeminiService _gemini;
    private readonly IInvestigationTeamProxy _proxy;

    public ChatController(ChatDbContext context, IGeminiService gemini, IInvestigationTeamProxy proxy)
    {
        _context = context;
        _gemini = gemini;
        _proxy = proxy;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _context.ChatSessions
            .Where(s => s.UserId == GetUserId())
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new { s.Id, s.AgentId, s.TeamId, s.Title, s.CreatedAt, s.UpdatedAt })
            .ToListAsync();
        return Ok(sessions);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(CreateSessionRequest request)
    {
        var title = request.Title ?? "New Chat";
        if (request.AgentId.HasValue)
        {
            var agent = await _proxy.GetAgentAsync(request.AgentId.Value);
            title = $"Chat with {agent?.Name ?? "Agent"}";
        }
        else if (request.TeamId.HasValue)
        {
            var team = await _proxy.GetTeamAsync(request.TeamId.Value);
            title = $"Chat with {team?.Name ?? "Team"}";
        }

        var session = new ChatSession
        {
            UserId = GetUserId(),
            AgentId = request.AgentId,
            TeamId = request.TeamId,
            Title = title
        };

        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMessages), new { id = session.Id }, new { session.Id, session.Title });
    }

    [HttpGet("sessions/{id}/messages")]
    public async Task<IActionResult> GetMessages(Guid id)
    {
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == GetUserId());
        if (session == null) return NotFound();

        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Id, m.Role, m.Content, m.CreatedAt })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost("sessions/{id}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, SendMessageRequest request)
    {
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == GetUserId());
        if (session == null) return NotFound();

        var user = await _context.Users.FirstAsync(u => u.Id == GetUserId());
        if (string.IsNullOrEmpty(user.GeminiApiKey))
            return BadRequest(new { message = "Gemini API key not configured. Update your profile." });

        string systemPrompt = "Eres un asistente de investigación útil y profesional.";
        if (session.AgentId.HasValue)
        {
            var agent = await _proxy.GetAgentAsync(session.AgentId.Value);
            if (agent != null)
            {
                var roleNames = new[] { "Investigador", "Analista", "Escritor", "Coordinador", "Revisor" };
                systemPrompt = $"Eres {agent.Name}, un {roleNames[agent.Role]} con habilidades en {string.Join(", ", agent.Skills)}. {agent.Description}";
            }
        }
        else if (session.TeamId.HasValue)
        {
            var team = await _proxy.GetTeamAsync(session.TeamId.Value);
            if (team != null)
            {
                var agents = new List<string>();
                foreach (var agentId in team.AgentIds)
                {
                    var agent = await _proxy.GetAgentAsync(agentId);
                    if (agent != null)
                    {
                        var roleNames = new[] { "Investigador", "Analista", "Escritor", "Coordinador", "Revisor" };
                        agents.Add($"{agent.Name} ({roleNames[agent.Role]})");
                    }
                }
                systemPrompt = $"Eres el equipo de investigación '{team.Name}'. {team.Description}. Miembros: {string.Join(", ", agents)}. Responde como el miembro más adecuado según el tema.";
            }
        }

        var history = await _context.ChatMessages
            .Where(m => m.SessionId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync();

        var historyTuples = history.Select(m => (m.Role, m.Content)).ToList();

        var userMessage = new ChatMessage
        {
            SessionId = id,
            Role = "user",
            Content = request.Content
        };
        _context.ChatMessages.Add(userMessage);
        await _context.SaveChangesAsync();

        string response;
        try
        {
            response = await _gemini.GenerateResponseAsync(user.GeminiApiKey, systemPrompt, historyTuples, request.Content);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = $"Error al comunicarse con Gemini: {ex.Message}" });
        }

        var assistantMessage = new ChatMessage
        {
            SessionId = id,
            Role = "assistant",
            Content = response
        };
        _context.ChatMessages.Add(assistantMessage);

        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { content = response });
    }

    [HttpDelete("sessions/{id}")]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == GetUserId());
        if (session == null) return NotFound();

        var messages = await _context.ChatMessages.Where(m => m.SessionId == id).ToListAsync();
        _context.ChatMessages.RemoveRange(messages);
        _context.ChatSessions.Remove(session);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Session deleted" });
    }
}
