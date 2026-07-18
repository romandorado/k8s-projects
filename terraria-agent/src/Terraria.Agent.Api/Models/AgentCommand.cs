namespace Terraria.Agent.Api.Models;

public class AgentCommand
{
    public string Command { get; set; } = string.Empty; // narrar, hora, clima, tiempo, invocar, consejo, peligro
    public string[] Args { get; set; } = [];
    public string Player { get; set; } = string.Empty;
}

public enum CommandType
{
    Narrar,
    Hora,
    Clima,
    Tiempo,
    Invocar,
    Consejo,
    Peligro,
    Unknown
}
