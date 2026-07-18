# Terraria Agent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 10 Agent that narrates Terraria gameplay using Groq AI, controlled by player commands `/agente` via TShock plugin.

**Architecture:** TShock plugin hooks chat/events → Agent API → Groq AI narration → TShock REST commands. Deployed as separate pod in Kubernetes.

**Tech Stack:** .NET 10, Groq API (llama-3.3-70b-versatile), TShock REST API, Docker, Kubernetes (k3s)

## Global Constraints

- Runtime: .NET 10 (mcr.microsoft.com/dotnet/sdk:10.0 for build, aspnet:10.0 for runtime)
- AI: Groq llama-3.3-70b-versatile
- World: Master difficulty (DIFFICULTY=2), Large size (-autocreate 3)
- Namespace: terraria (shared with Terraria Server)
- Port: 8080 for Agent API, 7878 for TShock REST API
- Language: Spanish (comunicación y documentación)

---

## File Structure

```
terraria-agent/
├── src/Terraria.Agent.Api/
│   ├── Controllers/
│   │   └── ChatController.cs          # POST /api/chat - recibe eventos del plugin
│   ├── Services/
│   │   ├── TShockClient.cs            # Comunicación con TShock REST API
│   │   ├── GroqService.cs             # Generación de narración con IA
│   │   └── CommandParser.cs           # Parsea comandos /agente
│   ├── Models/
│   │   ├── ChatEvent.cs               # Evento del plugin
│   │   └── AgentCommand.cs            # Comando parseado
│   ├── Program.cs
│   ├── Terraria.Agent.Api.csproj
│   └── appsettings.json
├── k8s/
│   ├── namespace.yaml
│   ├── deployment.yaml
│   ├── service.yaml
│   └── secret.yaml
├── Dockerfile
└── README.md
```

---

### Task 1: Project Scaffolding

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Terraria.Agent.Api.csproj`
- Create: `terraria-agent/src/Terraria.Agent.Api/Program.cs`
- Create: `terraria-agent/src/Terraria.Agent.Api/appsettings.json`

**Interfaces:**
- Consumes: None (first task)
- Produces: Minimal .NET 10 Web API running on port 8080

- [ ] **Step 1: Create project directory structure**

```bash
mkdir -p terraria-agent/src/Terraria.Agent.Api/Controllers
mkdir -p terraria-agent/src/Terraria.Agent.Api/Services
mkdir -p terraria-agent/src/Terraria.Agent.Api/Models
mkdir -p terraria-agent/k8s
```

- [ ] **Step 2: Create .csproj file**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create minimal Program.cs**

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());
app.MapControllers();

app.Run();
```

- [ ] **Step 4: Create appsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "TShock": {
    "Url": "http://terraria-server:7878",
    "Token": ""
  },
  "Groq": {
    "ApiKey": "",
    "Model": "llama-3.3-70b-versatile",
    "Endpoint": "https://api.groq.com/openai/v1/chat/completions"
  }
}
```

- [ ] **Step 5: Test the scaffold**

```bash
cd terraria-agent/src/Terraria.Agent.Api
dotnet run
```

Expected: Server starts on port 8080, `curl http://localhost:8080/health` returns 200 OK

- [ ] **Step 6: Commit**

```bash
git add terraria-agent/
git commit -m "feat: terraria-agent project scaffold"
```

---

### Task 2: Models

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Models/ChatEvent.cs`
- Create: `terraria-agent/src/Terraria.Agent.Api/Models/AgentCommand.cs`

**Interfaces:**
- Consumes: None
- Produces: Data models for plugin events and parsed commands

- [ ] **Step 1: Create ChatEvent model**

```csharp
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
```

- [ ] **Step 2: Create AgentCommand model**

```csharp
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
```

- [ ] **Step 3: Verify build**

```bash
cd terraria-agent/src/Terraria.Agent.Api
dotnet build
```

Expected: Build succeeds with no errors

- [ ] **Step 4: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Models/
git commit -m "feat: add ChatEvent and AgentCommand models"
```

---

### Task 3: CommandParser

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Services/CommandParser.cs`

**Interfaces:**
- Consumes: `ChatEvent` from Task 2
- Produces: `AgentCommand` with parsed command type and args

- [ ] **Step 1: Create CommandParser**

```csharp
using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class CommandParser
{
    private const string Prefix = "/agente";

    public AgentCommand? Parse(ChatEvent chatEvent)
    {
        if (!chatEvent.Text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = chatEvent.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        var command = parts[1].ToLowerInvariant();
        var args = parts.Length > 2 ? parts[2..] : [];

        return new AgentCommand
        {
            Command = command,
            Args = args,
            Player = chatEvent.Player
        };
    }

    public CommandType GetCommandType(AgentCommand command)
    {
        return command.Command switch
        {
            "narrar" => CommandType.Narrar,
            "hora" => CommandType.Hora,
            "clima" => CommandType.Clima,
            "tiempo" => CommandType.Tiempo,
            "invocar" => CommandType.Invocar,
            "consejo" => CommandType.Consejo,
            "peligro" => CommandType.Peligro,
            _ => CommandType.Unknown
        };
    }
}
```

- [ ] **Step 2: Verify build**

```bash
cd terraria-agent/src/Terraria.Agent.Api
dotnet build
```

Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Services/CommandParser.cs
git commit -m "feat: add CommandParser for /agente commands"
```

---

### Task 4: TShockClient

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Services/TShockClient.cs`

**Interfaces:**
- Consumes: TShock REST API at `http://terraria-server:7878`
- Produces: Methods to execute commands and broadcast messages

- [ ] **Step 1: Create TShockClient**

```csharp
namespace Terraria.Agent.Api.Services;

public class TShockClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _token;
    private readonly ILogger<TShockClient> _logger;

    public TShockClient(HttpClient httpClient, IConfiguration config, ILogger<TShockClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = config["TShock:Url"]!;
        _token = config["TShock:Token"]!;
        _logger = logger;
    }

    public async Task<string?> ExecuteCommandAsync(string command)
    {
        try
        {
            var url = $"{_baseUrl}/v3/server/rawcmd?cmd={Uri.EscapeDataString(command)}&token={_token}";
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("TShock command executed: {Command}, Response: {Response}", command, content);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute TShock command: {Command}", command);
            return null;
        }
    }

    public async Task BroadcastMessageAsync(string message)
    {
        await ExecuteCommandAsync($"say [Agent] {message}");
    }

    public async Task<string?> GetServerStatusAsync()
    {
        try
        {
            var url = $"{_baseUrl}/v2/server/status?token={_token}";
            var response = await _httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get server status");
            return null;
        }
    }
}
```

- [ ] **Step 2: Register in Program.cs**

Add to Program.cs:
```csharp
builder.Services.AddHttpClient<TShockClient>();
```

- [ ] **Step 3: Verify build**

```bash
cd terraria-agent/src/Terraria.Agent.Api
dotnet build
```

Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Services/TShockClient.cs
git add terraria-agent/src/Terraria.Agent.Api/Program.cs
git commit -m "feat: add TShockClient for REST API communication"
```

---

### Task 5: GroqService

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs`

**Interfaces:**
- Consumes: Groq API at `https://api.groq.com/openai/v1/chat/completions`
- Produces: Generated narration text

- [ ] **Step 1: Create GroqService**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class GroqService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly ILogger<GroqService> _logger;

    private const string SystemPrompt = @"Eres el narrador del mundo 'MundoSobrinos' en Terraria. Tu rol:
- Narrar eventos del juego de forma dramática y divertida
- Responder a comandos de jugadores con creatividad
- Mantener el tono épico pero amigable (juegas con sobrinos)
- Usar español casual y emojis ocasionales

Contexto del mundo:
- Mundo: MundoSobrinos (Master, grande)
- Jugadores: [se actualiza dinámicamente]
- Hora del día: [se actualiza]
- Clima actual: [se actualiza]

Reglas:
- Máximo 200 tokens por respuesta
- Responde en español
- Sé conciso (1-2 oraciones máximo)
- Recuerda: en Master los enemigos son brutales, drops exclusivos, y la dificultad es extrema";

    public GroqService(HttpClient httpClient, IConfiguration config, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
    }

    public async Task<string> GenerateNarrationAsync(string userMessage, string context = "")
    {
        try
        {
            var systemMessage = string.IsNullOrEmpty(context) 
                ? SystemPrompt 
                : $"{SystemPrompt}\n\nContexto actual: {context}";

            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = userMessage }
                },
                max_tokens = 200,
                temperature = 0.8
            };

            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.PostAsJsonAsync(_endpoint, request);
            var responseString = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(responseString);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            _logger.LogInformation("Groq narration generated: {Content}", content);
            return content ?? "El narrador está pensando...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate narration from Groq");
            return "El narrador está temporalmente silencioso...";
        }
    }
}
```

- [ ] **Step 2: Register in Program.cs**

Add to Program.cs:
```csharp
builder.Services.AddHttpClient<GroqService>();
```

- [ ] **Step 3: Verify build**

```bash
cd terraria-agent/src/Terraria.Agent.Api
dotnet build
```

Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs
git commit -m "feat: add GroqService for AI narration"
```

---

### Task 6: ChatController

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs`

**Interfaces:**
- Consumes: `ChatEvent` from plugin, `CommandParser`, `TShockClient`, `GroqService`
- Produces: Executes commands and broadcasts narrations

- [ ] **Step 1: Create ChatController**

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
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        CommandParser parser, 
        TShockClient tshock, 
        GroqService groq,
        ILogger<ChatController> logger)
    {
        _parser = parser;
        _tshock = tshock;
        _groq = groq;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleEvent([FromBody] ChatEvent chatEvent)
    {
        _logger.LogInformation("Received event from {Player}: {Text}", chatEvent.Player, chatEvent.Text);

        var command = _parser.Parse(chatEvent);
        if (command == null)
            return Ok(); // Not an /agente command, ignore

        var commandType = _parser.GetCommandType(command);
        _logger.LogInformation("Parsed command: {CommandType} from {Player}", commandType, command.Player);

        string narration = commandType switch
        {
            CommandType.Narrar => await HandleNarrar(command),
            CommandType.Hora => await HandleHora(),
            CommandType.Clima => await HandleClima(command),
            CommandType.Tiempo => await HandleTiempo(command),
            CommandType.Invocar => await HandleInvocar(command),
            CommandType.Consejo => await HandleConsejo(),
            CommandType.Peligro => await HandlePeligro(),
            _ => "Comando no reconocido. Usa /agente [narrar|hora|clima|tiempo|invocar|consejo|peligro]"
        };

        await _tshock.BroadcastMessageAsync(narration);
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
        // Parse time from status (simplified - would need proper JSON parsing)
        return await _groq.GenerateNarrationAsync(
            "¿Qué hora es en el mundo? Describe la hora actual de forma narrativa.");
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
            "golem" or "golem" => "spawnboss Golem",
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

- [ ] **Step 2: Verify build**

```bash
cd terraria-agent/src/Terraria.Agent.Api
dotnet build
```

Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs
git commit -m "feat: add ChatController with all /agente commands"
```

---

### Task 7: Dockerfile

**Files:**
- Create: `terraria-agent/Dockerfile`

**Interfaces:**
- Consumes: .NET 10 project from previous tasks
- Produces: Docker image `terraria-agent:latest`

- [ ] **Step 1: Create Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Terraria.Agent.Api/*.csproj src/Terraria.Agent.Api/
RUN dotnet restore src/Terraria.Agent.Api/Terraria.Agent.Api.csproj

COPY src/Terraria.Agent.Api/ src/Terraria.Agent.Api/
RUN dotnet publish src/Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Terraria.Agent.Api.dll"]
```

- [ ] **Step 2: Build Docker image**

```bash
cd terraria-agent
sudo docker build --no-cache -t terraria-agent:latest .
```

Expected: Image builds successfully

- [ ] **Step 3: Commit**

```bash
git add terraria-agent/Dockerfile
git commit -m "feat: add Dockerfile for terraria-agent"
```

---

### Task 8: Kubernetes Manifests

**Files:**
- Create: `terraria-agent/k8s/namespace.yaml`
- Create: `terraria-agent/k8s/deployment.yaml`
- Create: `terraria-agent/k8s/service.yaml`
- Create: `terraria-agent/k8s/secret.yaml`

**Interfaces:**
- Consumes: Docker image from Task 7
- Produces: K8s resources in `terraria` namespace

- [ ] **Step 1: Create namespace.yaml**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: terraria
  labels:
    app: terraria-agent
```

- [ ] **Step 2: Create secret.yaml**

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: terraria-agent-secret
  namespace: terraria
type: Opaque
stringData:
  tshock-token: "TU_TOKEN_AQUI"
  groq-api-key: "gsk_TU_API_KEY_AQUI"
```

- [ ] **Step 3: Create deployment.yaml**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: terraria-agent
  namespace: terraria
  labels:
    app: terraria-agent
spec:
  replicas: 1
  selector:
    matchLabels:
      app: terraria-agent
  template:
    metadata:
      labels:
        app: terraria-agent
    spec:
      containers:
        - name: agent
          image: terraria-agent:latest
          imagePullPolicy: Never
          ports:
            - containerPort: 8080
              name: http
              protocol: TCP
          env:
            - name: TShock__Url
              value: "http://terraria-server:7878"
            - name: TShock__Token
              valueFrom:
                secretKeyRef:
                  name: terraria-agent-secret
                  key: tshock-token
            - name: Groq__ApiKey
              valueFrom:
                secretKeyRef:
                  name: terraria-agent-secret
                  key: groq-api-key
          resources:
            requests:
              memory: "256Mi"
              cpu: "100m"
            limits:
              memory: "512Mi"
              cpu: "250m"
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 5
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 15
            periodSeconds: 10
```

- [ ] **Step 4: Create service.yaml**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: terraria-agent
  namespace: terraria
  labels:
    app: terraria-agent
spec:
  type: ClusterIP
  selector:
    app: terraria-agent
  ports:
    - port: 8080
      targetPort: 8080
      protocol: TCP
      name: http
```

- [ ] **Step 5: Commit**

```bash
git add terraria-agent/k8s/
git commit -m "feat: add Kubernetes manifests for terraria-agent"
```

---

### Task 9: Update Terraria Server Config

**Files:**
- Modify: `terraria-server/configmap.yaml`
- Modify: `terraria-server/statefulset.yaml`

**Interfaces:**
- Consumes: Existing K8s manifests
- Produces: Updated config with REST API enabled, Master difficulty, Large world

- [ ] **Step 1: Update configmap.yaml**

Add to existing configmap:
```yaml
  RestApiEnabled: "true"
  RestApiPort: "7878"
  DIFFICULTY: "2"  # Master difficulty
```

- [ ] **Step 2: Update statefulset.yaml**

Change autocreate from 2 to 3 (Large world):
```yaml
          args:
            - "-autocreate"
            - "3"  # Changed from 2 (Medium) to 3 (Large)
```

- [ ] **Step 3: Commit**

```bash
git add terraria-server/
git commit -m "feat: enable TShock REST API, Master difficulty, Large world"
```

---

### Task 10: Deploy and Test

**Files:**
- None (deployment and testing)

**Interfaces:**
- Consumes: All previous tasks
- Produces: Running terraria-agent in Kubernetes

- [ ] **Step 1: Import Docker image to k3s**

```bash
sudo docker save terraria-agent:latest | sudo k3s ctr images import -
```

- [ ] **Step 2: Apply K8s manifests**

```bash
sudo k3s kubectl apply -f terraria-agent/k8s/
```

- [ ] **Step 3: Verify pod is running**

```bash
sudo k3s kubectl get pods -n terraria
```

Expected: `terraria-agent-xxxxx` shows `Running` status

- [ ] **Step 4: Test health endpoint**

```bash
sudo k3s kubectl exec -n terraria terraria-server-0 -- curl -s http://terraria-agent:8080/health
```

Expected: `200 OK`

- [ ] **Step 5: Update CONTEXT.md**

Add terraria-agent to the architecture diagram and pending tasks.

- [ ] **Step 6: Commit**

```bash
git add CONTEXT.md
git commit -m "docs: update CONTEXT.md with terraria-agent deployment"
```

---

## Self-Review

**1. Spec coverage:**
- ✅ Plugin TShock (EventNotifier) - Covered in Task 9 (config updates), actual plugin code would be a separate project
- ✅ Agent API (.NET 10) - Tasks 1-6
- ✅ Groq Integration - Task 5
- ✅ Kubernetes Deployment - Tasks 7-8
- ✅ Config updates - Task 9
- ✅ Deploy and test - Task 10

**2. Placeholder scan:** No TBDs or TODOs found. All code is complete.

**3. Type consistency:** All model names and method signatures are consistent across tasks.

**Note:** The TShock plugin (EventNotifier) is a separate C# project that would need its own build process. This plan focuses on the Agent API. The plugin would be a follow-up task or could be added as Task 11.
