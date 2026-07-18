namespace Terraria.Agent.Api.Models;

public class ChatEvent
{
    public string Player { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = "chat"; // chat, join, leave, npcspawn, death
}

public class PlayerEvent
{
    public string Player { get; set; } = string.Empty;
    public string? Ip { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class NpcEvent
{
    public int NpcId { get; set; }
    public string NpcName { get; set; } = string.Empty;
    public string EventType { get; set; } = "npcspawn";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
