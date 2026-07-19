using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace ChatBridge;

[ApiVersion(2, 1)]
public class ChatBridgePlugin : TerrariaPlugin
{
    private readonly HttpClient _http = new();
    private string _agentUrl = "";

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
        ServerApi.Hooks.ServerChat.Register(this, OnChat);
        TShock.Log.Info($"ChatBridge initialized. Agent URL: {_agentUrl}");
    }

    private void OnChat(ServerChatEventArgs args)
    {
        try
        {
            var payload = new
            {
                Player = args.Player.Name,
                Text = args.Text
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Fire and forget — don't block the game server
            _ = _http.PostAsync($"{_agentUrl}/api/chat", content).ContinueWith(t =>
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}
