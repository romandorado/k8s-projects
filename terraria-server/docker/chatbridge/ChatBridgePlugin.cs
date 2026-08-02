using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Terraria.ID;
using TShockAPI.Hooks;

namespace ChatBridge;

[ApiVersion(2, 1)]
public class ChatBridgePlugin : TerrariaPlugin
{
    private readonly HttpClient _http = new();
    private string _agentUrl = "";
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    // Natural event transition tracking (polled on GameUpdate)
    private bool _wasRaining;
    private bool _wasBloodMoon;
    private bool _wasEclipse;
    private bool _wasSlimeRain;
    private int _lastInvasionType;

    // Narration throttling
    private DateTime _lastBossSpawnNarration = DateTime.MinValue;
    private DateTime _lastEventNarration = DateTime.MinValue;
    private const double EventCooldownSeconds = 20;

    // Track real connected players (k8s probes / empty slots must not fire join/leave)
    private readonly HashSet<string> _connectedPlayers = new(StringComparer.OrdinalIgnoreCase);

    public override string Name => "ChatBridge";
    public override string Author => "roman";
    public override Version Version => new(1, 0, 0);
    public override string Description => "Forwards in-game chat to the Terraria Agent for AI narration";

    public ChatBridgePlugin(Main game) : base(game)
    {
        Order = 1;
    }

    public override void Initialize()
    {
        _agentUrl = Environment.GetEnvironmentVariable("AGENT_URL") ?? "http://terraria-agent:8080";
        PlayerHooks.PlayerChat += OnPlayerChat;

        // Narration hooks
        GetDataHandlers.KillMe += OnPlayerDeath;
        ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
        ServerApi.Hooks.NpcSpawn.Register(this, OnNpcSpawn);
        ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
        ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
        ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

        StartCommandListener();
        TShock.Log.Info($"ChatBridge initialized. Agent URL: {_agentUrl}, Command listener on :7879");
    }

    private void OnPlayerChat(PlayerChatEventArgs args)
    {
        try
        {
            var playerName = args.Player?.Name ?? "Unknown";
            TShock.Log.Info($"ChatBridge: [{playerName}] {args.RawText}");
            SendToAgent(playerName, args.RawText);
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge: {ex.Message}");
        }
    }

    private void SendToAgent(string player, string text)
    {
        try
        {
            var payload = new { Player = player, Text = text };
            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_agentUrl}/api/chat")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Agent-Token", Environment.GetEnvironmentVariable("AGENT_TOKEN") ?? "terraria-agent-secret-token-2024");

            _ = _http.SendAsync(request).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    TShock.Log.Error($"ChatBridge: Failed to forward: {t.Exception?.InnerException?.Message}");
            });
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge: {ex.Message}");
        }
    }

    private void SendSystemEvent(string text)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastEventNarration).TotalSeconds < EventCooldownSeconds)
            return;
        _lastEventNarration = now;
        TShock.Log.Info($"ChatBridge system event: {text}");
        SendToAgent("Sistema", text);
    }

    // ---- Narration hooks ----

    private void OnPlayerDeath(object? sender, GetDataHandlers.KillMeEventArgs e)
    {
        try
        {
            if (e.Player == null) return;
            var name = e.Player.Name;
            var deathText = e.PlayerDeathReason.GetDeathText(name).ToString();
            SendSystemEvent(deathText);
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge KillMe: {ex.Message}");
        }
    }

    private void OnNpcKilled(NpcKilledEventArgs e)
    {
        try
        {
            if (e.npc == null || !e.npc.boss) return;
            SendSystemEvent($"¡El jefe {e.npc.FullName} ha sido derrotado!");
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge NpcKilled: {ex.Message}");
        }
    }

    private void OnNpcSpawn(NpcSpawnEventArgs e)
    {
        try
        {
            var npc = Main.npc[e.NpcId];
            if (npc == null || !npc.active || !npc.boss) return;
            var now = DateTime.UtcNow;
            if ((now - _lastBossSpawnNarration).TotalSeconds < 5)
                return;
            _lastBossSpawnNarration = now;
            SendSystemEvent($"Ha aparecido el jefe {npc.FullName}.");
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge NpcSpawn: {ex.Message}");
        }
    }

    private void OnServerJoin(JoinEventArgs e)
    {
        try
        {
            var p = TShock.Players[e.Who];
            if (p == null || !p.Active) return;
            if (!_connectedPlayers.Add(p.Name)) return;
            SendSystemEvent($"{p.Name} se ha unido al servidor.");
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge ServerJoin: {ex.Message}");
        }
    }

    private void OnServerLeave(LeaveEventArgs e)
    {
        try
        {
            var p = TShock.Players[e.Who];
            var name = p?.Name ?? "";
            if (string.IsNullOrEmpty(name) || !_connectedPlayers.Remove(name)) return;
            SendSystemEvent($"{name} ha abandonado el servidor.");
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge ServerLeave: {ex.Message}");
        }
    }

    private void OnGameUpdate(EventArgs e)
    {
        try
        {
            if (Main.raining && !_wasRaining) SendSystemEvent("Ha empezado a llover.");
            if (!Main.raining && _wasRaining) SendSystemEvent("La lluvia ha cesado.");
            if (Main.bloodMoon && !_wasBloodMoon) SendSystemEvent("¡Se levanta la Luna de Sangre!");
            if (Main.eclipse && !_wasEclipse) SendSystemEvent("¡Un eclipse solar se cierne sobre el mundo!");
            if (Main.slimeRain && !_wasSlimeRain) SendSystemEvent("¡Está lloviendo slimes!");
            if (Main.invasionType != _lastInvasionType && Main.invasionType != 0)
                SendSystemEvent($"{GetInvasionName(Main.invasionType)} invade el mundo!");

            _wasRaining = Main.raining;
            _wasBloodMoon = Main.bloodMoon;
            _wasEclipse = Main.eclipse;
            _wasSlimeRain = Main.slimeRain;
            _lastInvasionType = Main.invasionType;
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge GameUpdate: {ex.Message}");
        }
    }

    private static string GetInvasionName(int type) => type switch
    {
        1 => "Los goblins",
        2 => "La Legión de la Nieve",
        3 => "Los piratas",
        4 => "Los marcianos",
        _ => "Una fuerza desconocida"
    };

    private void StartCommandListener()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://+:7879/");
        _listener.Start();

        Task.Run(() => AcceptRequests(_cts.Token));
    }

    private async Task AcceptRequests(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener!.GetContextAsync();
                _ = Task.Run(() => HandleCommandRequest(context), ct);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex)
            {
                TShock.Log.Error($"ChatBridge listener: {ex.Message}");
            }
        }
    }

    private void HandleCommandRequest(HttpListenerContext context)
    {
        try
        {
            if (context.Request.HttpMethod != "POST")
            {
                context.Response.StatusCode = 405;
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            var body = reader.ReadToEnd();
            var doc = JsonDocument.Parse(body);
            var cmd = doc.RootElement.GetProperty("command").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(cmd))
            {
                context.Response.StatusCode = 400;
                var errBytes = Encoding.UTF8.GetBytes("{\"error\":\"empty command\"}");
                context.Response.OutputStream.Write(errBytes, 0, errBytes.Length);
                return;
            }

            var response = HandleBridgeCommand(cmd);

            var result = JsonSerializer.Serialize(new { status = "200", response });
            var resultBytes = Encoding.UTF8.GetBytes(result);
            context.Response.ContentType = "application/json";
            context.Response.OutputStream.Write(resultBytes, 0, resultBytes.Length);
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge exec error: {ex.Message}");
            context.Response.StatusCode = 500;
            var errBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
            context.Response.OutputStream.Write(errBytes, 0, errBytes.Length);
        }
        finally
        {
            context.Response.Close();
        }
    }

    private string HandleBridgeCommand(string command)
    {
        var lower = command.TrimStart('/').ToLower();
        if (lower.StartsWith("bridge "))
            lower = lower[7..];
        TShock.Log.Info($"ChatBridge bridge cmd: {lower}");

        // Slime Rain - special world event
        if (lower.StartsWith("slime rain off") || lower.StartsWith("slime rain stop") || lower == "slime off")
        {
            Main.slimeRain = false;
            Main.slimeRainTime = 0;
            NetMessage.SendData(56, -1, -1, null, 0);
            return "slime rain stopped";
        }

        if (lower.StartsWith("slime rain") || lower == "slime on")
        {
            Main.slimeRain = true;
            Main.slimeRainTime = 7200;
            NetMessage.SendData(56, -1, -1, null, 0);
            return "slime rain started";
        }

        // Rain commands
        if (lower.StartsWith("rain off") || lower.StartsWith("rain stop") || lower == "rain clear")
        {
            Main.raining = false;
            Main.maxRaining = 0f;
            Main.rainTime = 0;
            NetMessage.SendData(56, -1, -1, null, 0);
            return "rain stopped";
        }

        if (lower.StartsWith("rain heavy") || lower == "rain max")
        {
            Main.raining = true;
            Main.maxRaining = 1f;
            Main.rainTime = 3600;
            NetMessage.SendData(56, -1, -1, null, 0);
            return "heavy rain started";
        }

        if (lower.StartsWith("rain on") || lower.StartsWith("rain start") || lower == "rain")
        {
            Main.raining = true;
            Main.maxRaining = 0.5f;
            Main.rainTime = 3600;
            NetMessage.SendData(56, -1, -1, null, 0);
            return "rain started";
        }

        // Wind
        if (lower.StartsWith("wind "))
        {
            var parts = lower.Split(' ');
            if (parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var speed))
            {
                Main.windSpeedTarget = speed;
                NetMessage.SendData(56, -1, -1, null, 0);
                return $"wind set to {speed}";
            }
            return "invalid wind speed";
        }

        // Blood moon
        if (lower.StartsWith("bloodmoon on") || lower == "bloodmoon")
        {
            Main.bloodMoon = true;
            NetMessage.SendData(56, -1, -1, null, 0);
            return "blood moon started";
        }

        if (lower.StartsWith("bloodmoon off") || lower == "bloodmoon stop")
        {
            Main.bloodMoon = false;
            NetMessage.SendData(56, -1, -1, null, 0);
            return "blood moon stopped";
        }

        // Eclipse
        if (lower.StartsWith("eclipse on") || lower == "eclipse")
        {
            Main.eclipse = true;
            NetMessage.SendData(56, -1, -1, null, 0);
            return "eclipse started";
        }

        if (lower.StartsWith("eclipse off") || lower == "eclipse stop")
        {
            Main.eclipse = false;
            NetMessage.SendData(56, -1, -1, null, 0);
            return "eclipse stopped";
        }

        // Time commands - execute directly on server
        if (lower.StartsWith("time "))
        {
            var timeArg = lower[5..].Trim();
            switch (timeArg)
            {
                case "now":
                case "actual":
                    return GetCurrentTimeString();
                case "day":
                    Main.dayTime = true;
                    Main.time = 0;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "time set to day";
                case "night":
                    Main.dayTime = false;
                    Main.time = 0;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "time set to night";
                case "noon":
                    Main.dayTime = true;
                    Main.time = 27000;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "time set to noon";
                case "dusk":
                    Main.dayTime = false;
                    Main.time = 0;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "time set to dusk";
                case "midnight":
                    Main.dayTime = false;
                    Main.time = 16200;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "time set to midnight";
                default:
                    if (int.TryParse(timeArg, out var hour) && hour >= 0 && hour <= 23)
                        return SetTimeByHour(hour);
                    return $"unknown time: {timeArg}";
            }
        }

        // World event commands
        if (lower.StartsWith("worldevent "))
        {
            var evt = lower[11..].Trim();
            switch (evt)
            {
                case "meteor":
                    Commands.HandleCommand(TSPlayer.Server, "/worldevent meteor");
                    return "meteor summoned";
                case "bloodmoon":
                    Main.bloodMoon = true;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "blood moon started";
                case "eclipse":
                    Main.eclipse = true;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "eclipse started";
                case "fullmoon":
                    Main.bloodMoon = false;
                    Main.eclipse = false;
                    Main.dayTime = false;
                    Main.time = 16200;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "full moon set";
                case "sandstorm":
                    Main.windSpeedTarget = 20f;
                    Main.maxRaining = 0f;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "sandstorm started";
                case "invasion goblins":
                    Commands.HandleCommand(TSPlayer.Server, "/worldevent goblins");
                    return "goblin invasion started";
                case "invasion pirates":
                    Commands.HandleCommand(TSPlayer.Server, "/worldevent pirates");
                    return "pirate invasion started";
                case "invasion martians":
                    Commands.HandleCommand(TSPlayer.Server, "/worldevent martians");
                    return "martian invasion started";
                case "slime":
                    Main.slimeRain = true;
                    Main.slimeRainTime = 7200;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "slime rain started";
                case "lanternsnight":
                    Commands.HandleCommand(TSPlayer.Server, "/worldevent lanternsnight");
                    return "lantern night started";
                case "meteorshower":
                    Commands.HandleCommand(TSPlayer.Server, "/worldevent meteorshower");
                    return "meteor shower started";
                case "coinrain":
                    Main.raining = true;
                    Main.coinRain = 3600;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "coin rain started";
                case "star":
                    Commands.HandleCommand(TSPlayer.Server, "/star");
                    return "falling stars night started";
                case "halloween":
                    Main.halloween = true;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "halloween forced";
                case "xmas":
                    Main.xMas = true;
                    NetMessage.SendData(56, -1, -1, null, 0);
                    return "xmas forced";
                default:
                    return $"unknown event: {evt}";
            }
        }

        // Hardmode toggle
        if (lower == "hardmode")
        {
            Commands.HandleCommand(TSPlayer.Server, "/hardmode");
            return "hardmode toggled";
        }

        // Boss spawn - must be executed for each online player
        if (lower.StartsWith("spawnboss "))
        {
            var bossName = lower[10..].Trim();
            var players = TShock.Players.Where(p => p?.Active == true).ToList();
            if (players.Count == 0)
                return "no players online to spawn boss";

            var npcIds = GetBossTypes(bossName);
            foreach (var player in players)
            {
                foreach (var npcType in npcIds)
                {
                    var npcId = NPC.NewNPC(null, (int)player.TPlayer.position.X, (int)player.TPlayer.position.Y - 50, npcType);
                    if (npcId >= 0)
                    {
                        Main.npc[npcId].target = player.Index;
                        NetMessage.SendData(23, -1, -1, null, npcId);
                    }
                }
            }
            TShock.Log.Info($"ChatBridge: Spawned {bossName} ({string.Join(",", npcIds)}) for {players.Count} players");
            return $"boss {bossName} spawned for {players.Count} players";
        }

        // Fallback: execute as TShock command
        var fullCmd = command.StartsWith("/") ? command : $"/{command}";
        TShock.Log.Info($"ChatBridge exec TShock: {fullCmd}");
        Commands.HandleCommand(TSPlayer.Server, fullCmd);
        return "executed";
    }

    private static string SetTimeByHour(int hour)
    {
        const float dayStartHour = 4.5f;   // 4:30 AM
        const float nightStartHour = 19.5f; // 7:30 PM
        const float ticksPerHour = 3600f;

        if (hour >= dayStartHour && hour < nightStartHour)
        {
            Main.dayTime = true;
            Main.time = (int)((hour - dayStartHour) * ticksPerHour);
        }
        else
        {
            Main.dayTime = false;
            var nightHour = hour >= nightStartHour ? hour - nightStartHour : hour + 24 - nightStartHour;
            Main.time = (int)(nightHour * ticksPerHour);
        }
        NetMessage.SendData(56, -1, -1, null, 0);
        return $"time set to {hour:D2}:00";
    }

    private static string GetCurrentTimeString()
    {
        const double dayStartHour = 4.5;   // 4:30 AM
        const double nightStartHour = 19.5; // 7:30 PM
        const double ticksPerHour = 3600.0;

        double hourOfDay;
        if (Main.dayTime)
        {
            hourOfDay = dayStartHour + (Main.time / ticksPerHour);
        }
        else
        {
            hourOfDay = nightStartHour + (Main.time / ticksPerHour);
            if (hourOfDay >= 24.0) hourOfDay -= 24.0;
        }

        var h = (int)hourOfDay;
        var m = (int)((hourOfDay - h) * 60.0);
        var period = h >= 12 ? "PM" : "AM";
        var hour12 = h % 12 == 0 ? 12 : h % 12;
        return $"current time is {hour12}:{m:D2} {period}";
    }

    private static int[] GetBossTypes(string name)
    {
        return name.ToLower() switch
        {
            "kingslime" or "king slime" or "slime" => new[] { 50 },
            "eyeofcthulhu" or "eye of cthulhu" or "eye" or "ojo" => new[] { 4 },
            "eaterofworlds" or "eater of worlds" or "eater" or "gusano" => new[] { 13 },
            "skeletron" or "esqueleto" => new[] { 35 },
            "queenbee" or "queen bee" or "bee" or "abeja" => new[] { 222 },
            "thetwins" or "twins" or "gemelos" => new[] { 125, 126 },
            "thedestroyer" or "destroyer" or "destructor" => new[] { 134 },
            "skeletronprime" or "skeletron prime" or "primo" => new[] { 127 },
            "plantera" => new[] { 262 },
            "golem" => new[] { 245 },
            "lunaticcultist" or "lunatic cultist" or "cultista" => new[] { 439 },
            "moonlord" or "moon lord" or "moon" or "lord" or "señor" => new[] { 398 },
            "wallofflesh" or "wall of flesh" or "wall" or "muro" => new[] { 113 },
            _ => new[] { 50 }
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PlayerHooks.PlayerChat -= OnPlayerChat;
            GetDataHandlers.KillMe -= OnPlayerDeath;
            ServerApi.Hooks.NpcKilled.Deregister(this, OnNpcKilled);
            ServerApi.Hooks.NpcSpawn.Deregister(this, OnNpcSpawn);
            ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
            ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
            ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            _cts?.Cancel();
            _listener?.Stop();
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}
