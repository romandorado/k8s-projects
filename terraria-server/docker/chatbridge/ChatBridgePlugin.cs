using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace ChatBridge;

[ApiVersion(2, 1)]
public class ChatBridgePlugin : TerrariaPlugin
{
    private readonly HttpClient _http = new();
    private string _agentUrl = "";
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

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
        PlayerHooks.PlayerJoin += OnPlayerJoin;
        StartCommandListener();
        TShock.Log.Info($"ChatBridge initialized. Agent URL: {_agentUrl}, Command listener on :7879");
    }

    private void OnPlayerJoin(PlayerJoinEventArgs args)
    {
        try
        {
            var playerName = args.Player?.Name ?? "Unknown";
            var payload = new
            {
                Player = playerName,
                Text = "[EVENTO] " + playerName + " ha entrado al mundo. Salúdalo de forma épica y dramática.",
                Event = "join"
            };

            TShock.Log.Info($"ChatBridge: Player joined: {playerName}");

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_agentUrl}/api/chat")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Agent-Token", Environment.GetEnvironmentVariable("AGENT_TOKEN") ?? "terraria-agent-secret-token-2024");

            _ = _http.SendAsync(request).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    TShock.Log.Error($"ChatBridge: Failed to send join greeting: {t.Exception?.InnerException?.Message}");
            });
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge: {ex.Message}");
        }
    }

    private void OnPlayerChat(PlayerChatEventArgs args)
    {
        try
        {
            var playerName = args.Player?.Name ?? "Unknown";
            var payload = new
            {
                Player = playerName,
                Text = args.RawText
            };

            TShock.Log.Info($"ChatBridge: [{playerName}] {args.RawText}");

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_agentUrl}/api/chat")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Agent-Token", Environment.GetEnvironmentVariable("AGENT_TOKEN") ?? "terraria-agent-secret-token-2024");

            _ = _http.SendAsync(request).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    TShock.Log.Error($"ChatBridge: Failed to forward chat: {t.Exception?.InnerException?.Message}");
            });
        }
        catch (Exception ex)
        {
            TShock.Log.Error($"ChatBridge: {ex.Message}");
        }
    }

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

        var fullCmd = command.StartsWith("/") ? command : $"/{command}";
        TShock.Log.Info($"ChatBridge exec TShock: {fullCmd}");
        Commands.HandleCommand(TSPlayer.Server, fullCmd);
        return "executed";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            PlayerHooks.PlayerChat -= OnPlayerChat;
            _cts?.Cancel();
            _listener?.Stop();
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}
