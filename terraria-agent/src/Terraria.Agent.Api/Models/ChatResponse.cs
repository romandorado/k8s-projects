namespace Terraria.Agent.Api.Models;

/// <summary>
/// Response from the Terraria Agent after processing a chat message.
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// The agent's narration text (epic description of the event/action).
    /// </summary>
    /// <example>¡El sol brilla en el cielo de MundoSobrinos!</example>
    public string Narration { get; set; } = string.Empty;

    /// <summary>
    /// The TShock command that was executed (if any).
    /// </summary>
    /// <example>time day</example>
    public string? Action { get; set; }

    /// <summary>
    /// The command type if a structured command was used.
    /// </summary>
    /// <example>Hora</example>
    public string? Command { get; set; }
}
