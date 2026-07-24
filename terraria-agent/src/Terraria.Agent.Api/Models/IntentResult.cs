namespace Terraria.Agent.Api.Models;

public class IntentResult
{
    public bool Respond { get; set; } = true;
    public string? Action { get; set; }
    public string? Narration { get; set; }
}
