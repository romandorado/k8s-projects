namespace Terraria.Agent.Api.Models;

public class AutoEventConfig
{
    public int PollIntervalSeconds { get; set; } = 30;
    public int AmbientIntervalMinutes { get; set; } = 15;
    public int AmbientJitterMinutes { get; set; } = 2;
    public int BossCheckIntervalMinutes { get; set; } = 25;
    public int BossCheckJitterMinutes { get; set; } = 5;
    public int MinPlayersForBoss { get; set; } = 1;
}
