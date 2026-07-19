# Chat Forwarding + Natural Language Agent — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the Terraria Agent to the in-game chat via a TShock plugin, and make the agent understand natural language commands through Groq AI.

**Architecture:** A minimal TShock plugin (`ChatBridge`) captures all `ServerChat` events and POSTs them to the agent's `/api/chat` endpoint. The agent gains a new `IntentParser` service that uses Groq to interpret natural language and map it to TShock commands, while keeping the existing `/agente` command system intact.

**Tech Stack:** .NET 9 (TShock plugin), .NET 10 (agent), TShock 6.0.0 API, Groq API (llama-3.3-70b-versatile), Docker, Kubernetes (k3s)

## Global Constraints

- TShock 6.0.0 for Terraria 1.4.5.5 — plugin references from `/tshock/bin/` and `/tshock/ServerPlugins/`
- Agent runs .NET 10, targets `net10.0`
- Groq model: `llama-3.3-70b-versatile`, endpoint: `https://api.groq.com/openai/v1/chat/completions`
- TShock REST API base: `http://terraria-server:7878`
- Agent ClusterIP: `http://terraria-agent:8080`
- All communication in-cluster only
- Language: Spanish for all user-facing text

---

## File Structure

```
terraria-server/docker/
├── chatbridge/                          # NUEVO - Plugin project
│   ├── ChatBridge.csproj                # NUEVO
│   └── ChatBridgePlugin.cs              # NUEVO
├── Dockerfile                           # MODIFICAR - add plugin build step
└── bootstrap.sh                         # SIN CAMBIOS

terraria-server/
└── statefulset.yaml                     # MODIFICAR - add AGENT_URL env var

terraria-agent/src/Terraria.Agent.Api/
├── Services/
│   ├── IntentParser.cs                  # NUEVO - Groq natural language → action
│   ├── CommandParser.cs                 # SIN CAMBIOS
│   ├── TShockClient.cs                  # SIN CAMBIOS
│   └── GroqService.cs                   # SIN CAMBIOS
├── Controllers/
│   └── ChatController.cs                # MODIFICAR - add natural language flow
├── Models/
│   ├── AgentCommand.cs                  # SIN CAMBIOS
│   ├── ChatEvent.cs                     # SIN CAMBIOS
│   └── IntentResult.cs                  # NUEVO - Groq response model
└── Program.cs                           # SIN CAMBIOS

terraria-agent/
└── Dockerfile                           # SIN CAMBIOS
```

---

### Task 1: Create ChatBridge Plugin Project

**Files:**
- Create: `terraria-server/docker/chatbridge/ChatBridge.csproj`
- Create: `terraria-server/docker/chatbridge/ChatBridgePlugin.cs`

**Interfaces:**
- Produces: `ChatBridgePlugin` class with `TerrariaPlugin` base, hooks `ServerChat`, POSTs to agent `/api/chat`

- [ ] **Step 1: Create project directory**

```bash
mkdir -p /home/roman/k8s-projects/terraria-server/docker/chatbridge
```

- [ ] **Step 2: Create ChatBridge.csproj**

Write `terraria-server/docker/chatbridge/ChatBridge.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AssemblyName>ChatBridgePlugin</AssemblyName>
    <RootNamespace>ChatBridge</RootNamespace>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="ChatBridgePlugin.cs" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="TShockAPI">
      <HintPath>../tshock-ref/TShockAPI.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="TerrariaServer">
      <HintPath>../tshock-ref/TerrariaServer.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="OTAPI">
      <HintPath>../tshock-ref/OTAPI.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create ChatBridgePlugin.cs**

Write `terraria-server/docker/chatbridge/ChatBridgePlugin.cs`:

```csharp
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
```

- [ ] **Step 4: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-server/docker/chatbridge/
git commit -m "feat: ChatBridge TShock plugin — forwards chat to agent"
```

---

### Task 2: Modify TShock Dockerfile to Build Plugin

**Files:**
- Modify: `terraria-server/docker/Dockerfile`

**Interfaces:**
- Consumes: ChatBridge plugin source from Task 1
- Produces: TShock Docker image with `ChatBridgePlugin.dll` in `/tshock/ServerPlugins/`

- [ ] **Step 1: Modify Dockerfile**

Replace the contents of `terraria-server/docker/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0

RUN apt-get update && apt-get install -y \
    bash jq wget unzip libgdiplus sqlite3 python3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /tshock

RUN wget -q "https://github.com/Pryaxis/TShock/releases/download/v6.0.0/TShock-6.0.0-for-Terraria-1.4.5.5-linux-x64-Release.zip" -O tshock.zip && \
    unzip -q tshock.zip && \
    rm tshock.zip && \
    tar xf TShock-Beta-linux-x64-Release.tar && \
    rm TShock-Beta-linux-x64-Release.tar && \
    chmod +x TShock.Server

# Copy TShock DLLs for plugin compilation
RUN mkdir -p /tshock-ref && \
    cp /tshock/bin/TerrariaServer.dll /tshock-ref/ && \
    cp /tshock/bin/OTAPI.dll /tshock-ref/ && \
    cp /tshock/ServerPlugins/TShockAPI.dll /tshock-ref/

# Build ChatBridge plugin
COPY docker/chatbridge/ /tmp/chatbridge/
RUN dotnet publish /tmp/chatbridge/ChatBridge.csproj \
    -c Release \
    -o /tshock/ServerPlugins/ && \
    rm -rf /tmp/chatbridge /tshock-ref

COPY bootstrap.sh /tshock/bootstrap.sh
RUN chmod +x /tshock/bootstrap.sh

EXPOSE 7777 7878

ENTRYPOINT ["/bin/bash", "/tshock/bootstrap.sh"]
```

- [ ] **Step 2: Test Docker build**

```bash
cd /home/roman/k8s-projects/terraria-server
sudo docker build --no-cache -t terraria-tshock-1455:latest .
```

Expected: Build succeeds, image includes `ChatBridgePlugin.dll` in `/tshock/ServerPlugins/`

- [ ] **Step 3: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-server/docker/Dockerfile
git commit -m "feat: build ChatBridge plugin in TShock Docker image"
```

---

### Task 3: Add AGENT_URL to TShock StatefulSet

**Files:**
- Modify: `terraria-server/statefulset.yaml:30-48`

**Interfaces:**
- Consumes: Agent service name `terraria-agent` in namespace `terraria`
- Produces: env var `AGENT_URL=http://terraria-agent:8080` available in TShock container

- [ ] **Step 1: Add AGENT_URL env var**

In `terraria-server/statefulset.yaml`, add after line 48 (`REST_TOKEN`):

```yaml
            - name: AGENT_URL
              value: "http://terraria-agent:8080"
```

- [ ] **Step 2: Apply and redeploy**

```bash
cd /home/roman/k8s-projects/terraria-server
sudo k3s kubectl apply -f statefulset.yaml
sudo k3s kubectl rollout restart statefulset/terraria-server -n terraria
```

- [ ] **Step 3: Verify pod restarts**

```bash
sudo k3s kubectl get pods -n terraria -w
```

Wait for `terraria-server-0` to reach `1/1 Ready`. Check logs for ChatBridge init:

```bash
sudo k3s kubectl logs -n terraria terraria-server-0 --tail=20
```

Expected: log line containing `ChatBridge initialized. Agent URL: http://terraria-agent:8080`

- [ ] **Step 4: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-server/statefulset.yaml
git commit -m "feat: add AGENT_URL env var for ChatBridge plugin"
```

---

### Task 4: Create IntentParser Service in Agent

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Models/IntentResult.cs`
- Create: `terraria-agent/src/Terraria.Agent.Api/Services/IntentParser.cs`

**Interfaces:**
- Consumes: `ChatEvent` (from existing models), `HttpClient`, `IConfiguration`
- Produces: `IntentParser.ParseAsync(ChatEvent) → IntentResult?` (null means ignore, non-null means act+narrate)

- [ ] **Step 1: Create IntentResult model**

Write `terraria-agent/src/Terraria.Agent.Api/Models/IntentResult.cs`:

```csharp
namespace Terraria.Agent.Api.Models;

public class IntentResult
{
    public string? Action { get; set; }
    public string? Narration { get; set; }
}
```

- [ ] **Step 2: Create IntentParser service**

Write `terraria-agent/src/Terraria.Agent.Api/Services/IntentParser.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class IntentParser
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly ILogger<IntentParser> _logger;

    private const string SystemPrompt = @"Eres el narrador del mundo 'MundoSobrinos' en Terraria (Master difficulty, grande). Tu rol es narrar eventos del juego de forma dramática y divertida, en español casual.

Un jugador acaba de escribir algo en el chat. Analiza su mensaje y responde EXACTAMENTE con este JSON (sin texto extra, sin markdown, solo el JSON):

{
  ""action"": ""<comando_tshock_opcional>"",
  ""narration"": ""<tu respuesta narrativa>""
}

COMANDOS TSHOCK DISPONIBLES (usa solo estos valores exactos para 'action'):
Tiempo: ""time day"", ""time night"", ""time noon"", ""time dusk"", ""time midnight""
Clima: ""rain 0"" (normal), ""rain 1"" (lluvia), ""rain 2"" (nieve), ""rain 3"" (tormenta)
Eventos: ""bloodmoon"", ""eclipse"", ""fullmoon"", ""meteor""
Bosses: ""spawnboss KingSlime"", ""spawnboss EyeOfCthulhu"", ""spawnboss EaterOfWorlds"", ""spawnboss Skeletron"", ""spawnboss QueenBee"", ""spawnboss TheTwins"", ""spawnboss TheDestroyer"", ""spawnboss SkeletronPrime"", ""spawnboss Plantera"", ""spawnboss Golem"", ""spawnboss LunaticCultist"", ""spawnboss MoonLord""

REGLAS:
- Si el jugador pide algo que mapea a un comando TShock, ponlo en 'action'
- Si es solo conversación, 'action' es null
- 'narration' SIEMPRE debe tener texto (1-2 oraciones), NUNCA null
- Responde en español, tono épico pero amigable (juegas con sobrinos)
- Máximo 120 tokens
- Sé creativo pero conciso
- Si mencionan un boss, invócalo. Si piden clima, cámbialo. Si piden hora, cámbiala.";

    public IntentParser(HttpClient httpClient, IConfiguration config, ILogger<IntentParser> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
    }

    public async Task<IntentResult?> ParseAsync(ChatEvent chatEvent)
    {
        try
        {
            var userMessage = $"Jugador '{chatEvent.Player}' dice: {chatEvent.Text}";

            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = userMessage }
                },
                max_tokens = 120,
                temperature = 0.7
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseString);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
                return null;

            // Clean potential markdown wrapping
            var json = content.Trim();
            if (json.StartsWith("```"))
                json = json.Replace("```json", "").Replace("```", "").Trim();

            var result = JsonSerializer.Deserialize<IntentResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Intent parsed for {Player}: action={Action}, narration={Narration}",
                chatEvent.Player, result?.Action ?? "null",
                result?.Narration?[..Math.Min(50, result.Narration.Length)] ?? "null");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse intent from {Player}", chatEvent.Player);
            return null;
        }
    }
}
```

- [ ] **Step 3: Register IntentParser in Program.cs**

No changes needed — `ChatController` already has DI and `IntentParser` will be registered alongside existing services. (Registration happens in Task 5.)

- [ ] **Step 4: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-agent/src/Terraria.Agent.Api/Models/IntentResult.cs \
        terraria-agent/src/Terraria.Agent.Api/Services/IntentParser.cs
git commit -m "feat: IntentParser service — Groq natural language → TShock action"
```

---

### Task 5: Modify ChatController for Natural Language Flow

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs`
- Modify: `terraria-agent/src/Terraria.Agent.Api/Program.cs`

**Interfaces:**
- Consumes: `IntentParser` (from Task 4), `CommandParser` (existing), `TShockClient` (existing)
- Produces: Updated `HandleEvent` method that routes `/agente` commands and natural language

- [ ] **Step 1: Register IntentParser and IntentParser HttpClient in Program.cs**

Replace `terraria-agent/src/Terraria.Agent.Api/Program.cs`:

```csharp
using Terraria.Agent.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<CommandParser>();
builder.Services.AddHttpClient<TShockClient>();
builder.Services.AddHttpClient<GroqService>();
builder.Services.AddHttpClient<IntentParser>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());
app.MapControllers();

app.Run();
```

- [ ] **Step 2: Rewrite ChatController.HandleEvent**

Replace the entire `terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Terraria.Agent.Api.Models;
using Terraria.Agent.Api.Services;

namespace Terraria.Agent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly CommandParser _parser;
    private readonly TShockClient _tshock;
    private readonly GroqService _groq;
    private readonly IntentParser _intentParser;
    private readonly ILogger<ChatController> _logger;
    private readonly IConfiguration _config;

    public ChatController(
        CommandParser parser,
        TShockClient tshock,
        GroqService groq,
        IntentParser intentParser,
        ILogger<ChatController> logger,
        IConfiguration config)
    {
        _parser = parser;
        _tshock = tshock;
        _groq = groq;
        _intentParser = intentParser;
        _logger = logger;
        _config = config;
    }

    [HttpPost]
    public async Task<IActionResult> HandleEvent(
        [FromHeader(Name = "X-Agent-Token")] string? agentToken,
        [FromBody] ChatEvent chatEvent)
    {
        var expectedToken = _config["Agent:Token"];
        if (string.IsNullOrEmpty(expectedToken) || agentToken != expectedToken)
            return Unauthorized();

        _logger.LogInformation("Chat from {Player}: {Text}", chatEvent.Player, chatEvent.Text);

        // Route 1: /agente commands (existing system)
        var command = _parser.Parse(chatEvent);
        if (command != null)
        {
            return await HandleAgentCommand(command);
        }

        // Route 2: Natural language (IntentParser via Groq)
        var intent = await _intentParser.ParseAsync(chatEvent);
        if (intent == null || string.IsNullOrWhiteSpace(intent.Narration))
        {
            _logger.LogInformation("IntentParser: ignoring message from {Player}", chatEvent.Player);
            return Ok();
        }

        // Execute TShock action if detected
        if (!string.IsNullOrWhiteSpace(intent.Action))
        {
            _logger.LogInformation("Executing action: {Action}", intent.Action);
            await _tshock.ExecuteCommandAsync(intent.Action);
        }

        // Broadcast narration
        await _tshock.BroadcastMessageAsync($"[Narrador] {intent.Narration}");
        return Ok();
    }

    private async Task<IActionResult> HandleAgentCommand(AgentCommand command)
    {
        var commandType = _parser.GetCommandType(command);
        _logger.LogInformation("Agent command: {CommandType} from {Player}", commandType, command.Player);

        string narration;
        try
        {
            narration = commandType switch
            {
                CommandType.Narrar => await HandleNarrar(command),
                CommandType.Hora => await HandleHora(),
                CommandType.Clima => await HandleClima(command),
                CommandType.Tiempo => await HandleTiempo(command),
                CommandType.Invocar => await HandleInvocar(command),
                CommandType.Consejo => await HandleConsejo(),
                CommandType.Peligro => await HandlePeligro(),
                CommandType.Unknown when command.Command == "help" =>
                    "Comandos: /agente narrar|hora|clima|tiempo|invocar|consejo|peligro — o escribe libremente!",
                _ => "Comando no reconocido. Usa /agente [comando] o escribe libremente."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command {CommandType}", commandType);
            narration = "El narrador está temporalmente silencioso...";
        }

        await _tshock.BroadcastMessageAsync($"[Agent] {narration}");
        return Ok();
    }

    private async Task<string> HandleNarrar(AgentCommand command)
    {
        var scene = string.Join(" ", command.Args);
        return await _groq.GenerateNarrationAsync(
            $"El jugador {command.Player} pide narrar: {scene}");
    }

    private async Task<string> HandleHora()
    {
        var status = await _tshock.GetServerStatusAsync();
        var context = "No se pudo obtener el estado del servidor.";
        if (status != null)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(status);
                var root = doc.RootElement;
                var dayTime = root.TryGetProperty("dayTime", out var dt) && dt.GetBoolean();
                var bloodmoon = root.TryGetProperty("bloodmoon", out var bm) && bm.GetBoolean();
                var eclipse = root.TryGetProperty("eclipse", out var ec) && ec.GetBoolean();
                var timeOfDay = dayTime ? "de día" : "de noche";
                if (bloodmoon) timeOfDay += " con luna de sangre";
                if (eclipse) timeOfDay += " con eclipse";
                context = $"El mundo está {timeOfDay}.";
            }
            catch (System.Text.Json.JsonException)
            {
                context = "No se pudo parsear el estado del servidor.";
            }
        }
        return await _groq.GenerateNarrationAsync(
            $"¿Qué hora es en el mundo? El mundo está {context} Describe la hora actual de forma narrativa.",
            context);
    }

    private async Task<string> HandleClima(AgentCommand command)
    {
        var climate = command.Args.Length > 0 ? string.Join(" ", command.Args) : "normal";
        var tshockCmd = climate.ToLower() switch
        {
            "lluvia" => "rain 1",
            "nieve" => "rain 2",
            "tormenta" => "rain 3",
            "normal" => "rain 0",
            _ => $"rain {climate}"
        };

        await _tshock.ExecuteCommandAsync(tshockCmd);
        return await _groq.GenerateNarrationAsync(
            $"El clima cambia a {climate}. Narra el cambio de clima de forma dramática.");
    }

    private async Task<string> HandleTiempo(AgentCommand command)
    {
        var time = command.Args.Length > 0 ? command.Args[0] : "day";
        var tshockCmd = time.ToLower() switch
        {
            "dia" or "day" => "time day",
            "noche" or "night" => "time night",
            "mediodia" or "noon" => "time noon",
            "atardecer" or "dusk" => "time dusk",
            "medianoche" or "midnight" => "time midnight",
            _ => $"time {time}"
        };

        await _tshock.ExecuteCommandAsync(tshockCmd);
        return await _groq.GenerateNarrationAsync(
            $"El tiempo cambia a {time}. Narra el cambio de hora de forma dramática.");
    }

    private async Task<string> HandleInvocar(AgentCommand command)
    {
        var boss = command.Args.Length > 0 ? string.Join(" ", command.Args) : "king slime";
        var tshockCmd = boss.ToLower() switch
        {
            "wall of flesh" or "wall" or "muro" => "spawnboss WallOfFlesh",
            "king slime" or "slime" or "slim" => "spawnboss KingSlime",
            "eye of cthulhu" or "eye" or "ojo" => "spawnboss EyeOfCthulhu",
            "eater of worlds" or "eater" or "gusano" => "spawnboss EaterOfWorlds",
            "skeletron" or "esqueleto" => "spawnboss Skeletron",
            "queen bee" or "bee" or "abeja" => "spawnboss QueenBee",
            "twins" or "gemelos" => "spawnboss TheTwins",
            "destroyer" or "destructor" => "spawnboss TheDestroyer",
            "prime" or "skeletron prime" or "primo" => "spawnboss SkeletronPrime",
            "plantera" => "spawnboss Plantera",
            "golem" => "spawnboss Golem",
            "lunatic" or "lunatic cultist" or "cultista" => "spawnboss LunaticCultist",
            "moon lord" or "moon" or "lord" or "señor" => "spawnboss MoonLord",
            _ => $"spawnboss {boss}"
        };

        await _tshock.ExecuteCommandAsync(tshockCmd);
        return await _groq.GenerateNarrationAsync(
            $"¡El jugador {command.Player} ha invocado a {boss}! Narra la aparición del jefe de forma épica y dramática.");
    }

    private async Task<string> HandleConsejo()
    {
        return await _groq.GenerateNarrationAsync(
            "Da un consejo útil para jugar Terraria en dificultad Master. Sé conciso y dramático.");
    }

    private async Task<string> HandlePeligro()
    {
        return await _groq.GenerateNarrationAsync(
            "¡Advertencia de peligro! Narra una amenaza inminente de forma dramática.");
    }
}
```

- [ ] **Step 3: Build agent Docker image**

```bash
cd /home/roman/k8s-projects/terraria-agent
sudo docker build --no-cache -t terraria-agent:latest .
```

Expected: Build succeeds.

- [ ] **Step 4: Import and redeploy agent**

```bash
sudo docker save terraria-agent:latest | sudo k3s ctr images import -
sudo k3s kubectl rollout restart deployment/terraria-agent -n terraria
```

- [ ] **Step 5: Verify agent pod is Running**

```bash
sudo k3s kubectl get pods -n terraria -l app=terraria-agent
```

Expected: `terraria-agent-xxxxx 1/1 Ready`

- [ ] **Step 6: Test health endpoint**

```bash
sudo k3s kubectl exec -n terraria terraria-server-0 -- curl -s http://terraria-agent:8080/health
```

Expected: `200 OK`

- [ ] **Step 7: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-agent/
git commit -m "feat: natural language flow — IntentParser + ChatController routing"
```

---

### Task 6: End-to-End Test

**Files:** None (testing only)

**Interfaces:**
- Consumes: Running TShock server with ChatBridge plugin, running Agent with IntentParser
- Produces: Verified working chat forwarding + natural language responses

- [ ] **Step 1: Verify ChatBridge plugin loaded**

```bash
sudo k3s kubectl logs -n terraria terraria-server-0 --tail=30 | grep -i chatbridge
```

Expected: `ChatBridge initialized. Agent URL: http://terraria-agent:8080`

- [ ] **Step 2: Test via REST API (simulated chat)**

```bash
curl -s -X POST http://localhost:32445/api/chat \
  -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"Player": "roman", "Text": "haz que llueva"}'
```

Expected: `200 OK` — agent processes intent, executes `rain 1`, broadcasts narration

- [ ] **Step 3: Test /agente command via REST API**

```bash
curl -s -X POST http://localhost:32445/api/chat \
  -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"Player": "roman", "Text": "/agente hora"}'
```

Expected: `200 OK` — agent describes the time narratively

- [ ] **Step 4: Test natural language conversation (no action)**

```bash
curl -s -X POST http://localhost:32445/api/chat \
  -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"Player": "roman", "Text": "buenas noches a todos"}'
```

Expected: `200 OK` — agent responds narratively (no TShock action)

- [ ] **Step 5: Test in-game**

Connect to the server with the Terraria client and type messages in chat:
- "haz que llueva" → should see rain start + narration
- "invoca al ojo" → should spawn Eye of Cthulhu + epic narration
- "buenas" → narrator may or may not respond (30% chance)

- [ ] **Step 6: Update CONTEXT.md**

Update `k8s-projects/CONTEXT.md` with the new architecture and features.
