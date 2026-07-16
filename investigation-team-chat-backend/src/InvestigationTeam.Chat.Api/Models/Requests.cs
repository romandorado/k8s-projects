namespace InvestigationTeam.Chat.Api.Models;

public record RegisterRequest(string Email, string Password, string GeminiApiKey);
public record LoginRequest(string Email, string Password);
public record UpdateProfileRequest(string? Email, string? GeminiApiKey);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record CreateSessionRequest(Guid? AgentId, Guid? TeamId, string? Title);
public record SendMessageRequest(string Content);
