using System.ComponentModel.DataAnnotations;

namespace InvestigationTeam.Chat.Api.Models;

public class RegisterRequest
{
    [Required]
    public string Email { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";
    public string? GeminiApiKey { get; set; }
}

public class LoginRequest
{
    [Required]
    public string Email { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";
}

public class UpdateProfileRequest
{
    public string? Email { get; set; }
    public string? GeminiApiKey { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = "";
    [Required]
    public string NewPassword { get; set; } = "";
}

public class CreateSessionRequest
{
    public Guid? AgentId { get; set; }
    public Guid? TeamId { get; set; }
    public string? Title { get; set; }
}

public class SendMessageRequest
{
    [Required]
    public string Content { get; set; } = "";
}
