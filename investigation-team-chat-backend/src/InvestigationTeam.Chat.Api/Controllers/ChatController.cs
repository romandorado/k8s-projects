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

    private static readonly string[] RoleNames = ["Investigador", "Analista", "Escritor", "Coordinador", "Revisor"];

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Invalid token");
        return userId;
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var userId = GetUserId();
        var sessions = await _context.ChatSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new { s.Id, s.AgentId, s.TeamId, s.Title, s.CreatedAt, s.UpdatedAt })
            .ToListAsync();
        return Ok(sessions);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(CreateSessionRequest request)
    {
        var userId = GetUserId();
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
            UserId = userId,
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
        var userId = GetUserId();
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
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
        var userId = GetUserId();
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (session == null) return NotFound();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized(new { message = "User not found" });
        if (string.IsNullOrEmpty(user.GeminiApiKey))
            return BadRequest(new { message = "Gemini API key not configured. Update your profile." });

        string systemPrompt = "Eres un asistente de investigación útil y profesional.";
        if (session.AgentId.HasValue)
        {
            var agent = await _proxy.GetAgentAsync(session.AgentId.Value);
            if (agent != null)
            {
                systemPrompt = $"Eres {agent.Name}, un {(agent.Role >= 0 && agent.Role < RoleNames.Length ? RoleNames[agent.Role] : "Desconocido")} con habilidades en {string.Join(", ", agent.Skills)}. {agent.Description}";
            }

            var memories = await _context.AgentMemories
                .Where(m => m.AgentId == session.AgentId.Value)
                .OrderByDescending(m => m.CreatedAt)
                .Take(50)
                .Select(m => m.Content)
                .ToListAsync();

            if (memories.Count > 0)
            {
                systemPrompt += "\n\n## Memoria acumulada de conversaciones anteriores:\n" + string.Join("\n- ", memories);
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
                        agents.Add($"{agent.Name} ({(agent.Role >= 0 && agent.Role < RoleNames.Length ? RoleNames[agent.Role] : "Desconocido")})");
                    }
                }
                systemPrompt = $"Eres el equipo de investigación '{team.Name}'. {team.Description}. Miembros: {string.Join(", ", agents)}. Responde como el miembro más adecuado según el tema.";
            }
        }

        var history = await _context.ChatMessages
            .Where(m => m.SessionId == id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
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
            return StatusCode(502, new { message = $"Error al comunicarse con Groq: {ex.Message}" });
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

        if (session.AgentId.HasValue)
        {
            var messageCount = await _context.ChatMessages.CountAsync(m => m.SessionId == id);
            if (messageCount % 20 == 0 && messageCount >= 20)
            {
                _ = ExtractAndSaveMemoriesAsync(session.AgentId.Value, id, user.GeminiApiKey);
            }
        }

        return Ok(new { content = response });
    }

    private async Task ExtractAndSaveMemoriesAsync(Guid agentId, Guid sessionId, string apiKey)
    {
        try
        {
            await Task.Delay(5000);

            var recentMessages = await _context.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { m.Role, m.Content })
                .ToListAsync();

            if (recentMessages.Count < 4) return;

            var conversation = string.Join("\n", recentMessages.Select(m => $"{m.Role}: {m.Content}"));
            var extractionPrompt = "Analiza esta conversación y extrae SOLO los hechos relevantes y persistentes sobre el usuario. " +
                "Incluye: nombre del usuario, preferencias, proyectos en los que trabaja, technologías que usa, " +
                "decisiones tomadas, contexto importante. " +
                "NO incluyas: saludos, despedidas, preguntas generales, o información que ya esté en la descripción del agente. " +
                "Responde con una lista de hechos cortos (uno por línea), máx 5 hechos nuevos.";

            var facts = await _gemini.GenerateResponseAsync(apiKey, extractionPrompt, [], conversation);

            if (string.IsNullOrWhiteSpace(facts)) return;

            var existingMemories = await _context.AgentMemories
                .Where(m => m.AgentId == agentId)
                .Select(m => m.Content)
                .ToListAsync();

            foreach (var line in facts.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var fact = line.Trim().TrimStart('-', '*', '•').Trim();
                if (fact.Length < 10) continue;
                if (existingMemories.Any(em => em.Contains(fact, StringComparison.OrdinalIgnoreCase) || fact.Contains(em, StringComparison.OrdinalIgnoreCase))) continue;

                _context.AgentMemories.Add(new AgentMemory
                {
                    AgentId = agentId,
                    Content = fact
                });
            }

            await _context.SaveChangesAsync();
        }
        catch
        {
        }
    }

    [HttpDelete("sessions/{id}")]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        var userId = GetUserId();
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (session == null) return NotFound();

        await _context.ChatMessages.Where(m => m.SessionId == id).ExecuteDeleteAsync();
        _context.ChatSessions.Remove(session);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("agents/{agentId}/memories")]
    public async Task<IActionResult> GetAgentMemories(Guid agentId)
    {
        var memories = await _context.AgentMemories
            .Where(m => m.AgentId == agentId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { m.Id, m.Content, m.CreatedAt })
            .ToListAsync();
        return Ok(memories);
    }

    [HttpDelete("agents/{agentId}/memories/{memoryId}")]
    public async Task<IActionResult> DeleteAgentMemory(Guid agentId, Guid memoryId)
    {
        var memory = await _context.AgentMemories.FirstOrDefaultAsync(m => m.Id == memoryId && m.AgentId == agentId);
        if (memory == null) return NotFound();
        _context.AgentMemories.Remove(memory);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("agents/{agentId}/memories")]
    public async Task<IActionResult> ClearAgentMemories(Guid agentId)
    {
        await _context.AgentMemories.Where(m => m.AgentId == agentId).ExecuteDeleteAsync();
        return NoContent();
    }
}
