namespace Terraria.Agent.Api.Models;

public class TerrariaStatus
{
    public bool DayTime { get; set; }
    public bool BloodMoon { get; set; }
    public bool Eclipse { get; set; }
    public List<PlayerInfo> Players { get; set; } = new();
}

public class PlayerInfo
{
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }
}
