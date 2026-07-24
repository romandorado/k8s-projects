# Auto Events System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign AutoEventService to be context-aware, hybrid (cron + reactive), and AI-driven for boss spawning, with zero Groq calls when no players are online.

**Architecture:** A BackgroundService polls TShock every 30s for game state. When transitions are detected (day→night, bloodmoon, etc.) and players are online, it narrates via Groq. Ambient events fire every 15-20 min. Boss decisions are AI-driven every 25 min. Skip logic ensures no Groq calls when the server is empty.

**Tech Stack:** .NET 10, TShock 6.0.0 REST API, Groq API (llama-3.1-8b-instant), Docker, Kubernetes (k3s)

## Global Constraints

- Agent runs .NET 10, targets `net10.0`
- Groq model: `llama-3.1-8b-instant`, endpoint: `https://api.groq.com/openai/v1/chat/completions`
- TShock REST API base: `http://terraria-server:7878`
- TShock plugin port: `http://terraria-server:7879` (for spawnboss, bridge commands)
- TShock REST token: from K8s secret `terraria-agent-secret` key `tshock-token`
- Agent auth token: from same secret key `tshock-token`
- All communication in-cluster only
- Language: Spanish for all user-facing text

---

## File Structure

```
terraria-agent/src/Terraria.Agent.Api/
├── Services/
│   ├── AutoEventService.cs           # REWRITE - game state tracking, transitions, ambient, boss
│   ├── TShockClient.cs               # MODIFY - add GetStatusAsync (typed), SpawnBossAsync
│   └── GroqService.cs                # MODIFY - add GenerateEventNarrationAsync
├── Models/
│   └── AutoEventConfig.cs            # NEW - config POCO for AutoEvent section
├── Controllers/
│   └── ChatController.cs             # SIN CAMBIOS
├── appsettings.json                  # MODIFY - add AutoEvent section
└── Program.cs                        # SIN CAMBIOS (AutoEventService already registered)

terraria-agent/k8s/
└── deployment.yaml                   # MODIFY - add AutoEvent__* env vars

terraria-agent/
└── Dockerfile                        # SIN CAMBIOS
```

---

### Task 1: Add AutoEvent config model + appsettings section

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Models/AutoEventConfig.cs`
- Modify: `terraria-agent/src/Terraria.Agent.Api/appsettings.json`

**Interfaces:**
- Produces: `AutoEventConfig` class consumed by `AutoEventService`

- [ ] **Step 1: Create AutoEventConfig.cs**

```csharp
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
```

- [ ] **Step 2: Add AutoEvent section to appsettings.json**

Modify `terraria-agent/src/Terraria.Agent.Api/appsettings.json` — add after the `Groq` section:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Agent": {
    "Token": "terraria-agent-secret-token-2024"
  },
  "TShock": {
    "Url": "http://terraria-server:7878",
    "Token": "terraria-agent-secret-token-2024",
    "PluginUrl": "http://terraria-server:7879"
  },
  "Groq": {
    "ApiKey": "",
    "Model": "llama-3.1-8b-instant",
    "Endpoint": "https://api.groq.com/openai/v1/chat/completions"
  },
  "AutoEvent": {
    "PollIntervalSeconds": 30,
    "AmbientIntervalMinutes": 15,
    "AmbientJitterMinutes": 2,
    "BossCheckIntervalMinutes": 25,
    "BossCheckJitterMinutes": 5,
    "MinPlayersForBoss": 1
  }
}
```

- [ ] **Step 3: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-agent/src/Terraria.Agent.Api/Models/AutoEventConfig.cs terraria-agent/src/Terraria.Agent.Api/appsettings.json
git commit -m "feat: add AutoEvent config model and appsettings section"
```

---

### Task 2: Add typed server status model + TShockClient methods

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Services/TShockClient.cs`
- Create: `terraria-agent/src/Terraria.Agent.Api/Models/TerrariaStatus.cs`

**Interfaces:**
- Consumes: nothing new
- Produces: `TShockClient.GetStatusAsync()` returns `TerrariaStatus?`, `TShockClient.SpawnBossAsync(string boss)` returns `string?`

- [ ] **Step 1: Create TerrariaStatus.cs**

```csharp
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
```

- [ ] **Step 2: Add GetStatusAsync to TShockClient.cs**

Add this method after `GetServerStatusAsync`:

```csharp
public async Task<TerrariaStatus?> GetStatusAsync()
{
    try
    {
        var url = $"{_baseUrl}/v2/server/status?token={Uri.EscapeDataString(_token)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var status = new TerrariaStatus
        {
            DayTime = root.TryGetProperty("dayTime", out var dt) && dt.GetBoolean(),
            BloodMoon = root.TryGetProperty("bloodmoon", out var bm) && bm.GetBoolean(),
            Eclipse = root.TryGetProperty("eclipse", out var ec) && ec.GetBoolean()
        };

        if (root.TryGetProperty("players", out var playersArr))
        {
            foreach (var p in playersArr.EnumerateArray())
            {
                status.Players.Add(new PlayerInfo
                {
                    Name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Id = p.TryGetProperty("id", out var id) ? id.GetInt32() : 0
                });
            }
        }

        _logger.LogInformation("Server status: dayTime={DayTime}, blood={Blood}, eclipse={Eclipse}, players={Count}",
            status.DayTime, status.BloodMoon, status.Eclipse, status.Players.Count);

        return status;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get server status");
        return null;
    }
}
```

- [ ] **Step 3: Add SpawnBossAsync to TShockClient.cs**

Add this method after `ExecuteViaPlugin`:

```csharp
public async Task<string?> SpawnBossAsync(string boss)
{
    var cmd = $"/spawnboss {boss}";
    _logger.LogInformation("Spawning boss: {Boss}", boss);
    return await ExecuteViaPlugin(cmd);
}
```

- [ ] **Step 4: Add using for Models namespace**

At the top of `TShockClient.cs`, ensure this using exists:

```csharp
using Terraria.Agent.Api.Models;
```

- [ ] **Step 5: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-agent/src/Terraria.Agent.Api/Services/TShockClient.cs terraria-agent/src/Terraria.Agent.Api/Models/TerrariaStatus.cs
git commit -m "feat: add typed status model and SpawnBoss/TShockClient methods"
```

---

### Task 3: Add event narration method to GroqService

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs`

**Interfaces:**
- Consumes: `TerrariaStatus` from Task 2
- Produces: `GroqService.GenerateEventNarrationAsync(string prompt, TerrariaStatus status)` returns `string`

- [ ] **Step 1: Add GenerateEventNarrationAsync to GroqService.cs**

Add this method after `GenerateNarrationAsync`:

```csharp
public async Task<string> GenerateEventNarrationAsync(string prompt, TerrariaStatus? status = null)
{
    try
    {
        var context = status != null
            ? $"Mundo: MundoSobrinos (Master). Hora: {(status.DayTime ? "día" : "noche")}. " +
              $"Eventos: {(status.BloodMoon ? "luna de sangre " : "")}{(status.Eclipse ? "eclipse " : "")}. " +
              $"Jugadores: {string.Join(", ", status.Players.Select(p => p.Name))}."
            : "Mundo: MundoSobrinos (Master). Sin info de estado.";

        var systemMessage = @"Eres el narrador épico del mundo 'MundoSobrinos' en Terraria (Master difficulty).
Sé dramático, gracioso y exagerado. Juegas con sobrinos. Español casual.
Máximo 100 tokens por respuesta. Sé CORTO y ÉPICO.";

        var request = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = $"{systemMessage}\n\nContexto: {context}" },
                new { role = "user", content = prompt }
            },
            max_tokens = 100,
            temperature = 0.8
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
            _logger.LogError("Groq event API returned {StatusCode}", response.StatusCode);
            return "El narrador guarda silencio...";
        }

        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        _logger.LogInformation("Event narration: {Content}", content);
        return content ?? "El narrador guarda silencio...";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to generate event narration");
        return "El narrador guarda silencio...";
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs
git commit -m "feat: add context-aware event narration to GroqService"
```

---

### Task 4: Rewrite AutoEventService with game state tracking

**Files:**
- Rewrite: `terraria-agent/src/Terraria.Agent.Api/Services/AutoEventService.cs`

**Interfaces:**
- Consumes: `TShockClient.GetStatusAsync()`, `TShockClient.BroadcastMessageAsync()`, `TShockClient.SpawnBossAsync()`, `GroqService.GenerateEventNarrationAsync()`, `AutoEventConfig`

- [ ] **Step 1: Write the complete AutoEventService.cs**

Replace the entire content of `terraria-agent/src/Terraria.Agent.Api/Services/AutoEventService.cs`:

```csharp
using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class AutoEventService : BackgroundService
{
    private readonly TShockClient _tshock;
    private readonly GroqService _groq;
    private readonly AutoEventConfig _config;
    private readonly ILogger<AutoEventService> _logger;
    private readonly Random _random = new();

    private bool? _lastDayTime;
    private bool? _lastBloodMoon;
    private bool? _lastEclipse;

    private DateTime _nextAmbientTime;
    private DateTime _nextBossCheckTime;

    private static readonly string[] AmbientScenarios = new[]
    {
        "Un viento suave mueve las hojas del bosque cercano. Narra la brisa.",
        "Se escuchan grillos en la distancia. Narra la tranquilidad nocturna.",
        "Un rayo de sol atraviesa las nubes. Narra la luz dorada.",
        "Las luciérnagas bailan cerca del agua. Narra la magia del momento.",
        "Un murciélago pasa volando. Narra la presencia inesperada.",
        "El mar hace eco contra las rocas. Narra la calma del océano.",
        "Una hoja cae lentamente del árbol. Narra el cambio de estación.",
        "Se escucha un aullido lejano. Narra la misteriosa criatura.",
        "Las estrellas brillan con fuerza. Narra la inmensidad del cielo.",
        "Un cohete estrella cruza el cielo. Narra el momento efímero."
    };

    public AutoEventService(
        TShockClient tshock,
        GroqService groq,
        IConfiguration config,
        ILogger<AutoEventService> logger)
    {
        _tshock = tshock;
        _groq = groq;
        _logger = logger;

        _config = new AutoEventConfig();
        config.GetSection("AutoEvent").Bind(_config);

        _nextAmbientTime = DateTime.UtcNow.AddMinutes(_config.AmbientIntervalMinutes);
        _nextBossCheckTime = DateTime.UtcNow.AddMinutes(_config.BossCheckIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AutoEventService started. Poll={Poll}s, Ambient={Ambient}min, Boss={Boss}min",
            _config.PollIntervalSeconds, _config.AmbientIntervalMinutes, _config.BossCheckIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds), stoppingToken);

                var status = await _tshock.GetStatusAsync();
                if (status == null)
                {
                    _logger.LogWarning("Could not get server status, skipping tick");
                    continue;
                }

                var playerCount = status.Players.Count;

                // Always track state changes (even with 0 players)
                DetectTransitions(status);

                // Skip Groq calls if no players
                if (playerCount == 0)
                {
                    _logger.LogDebug("No players online, skipping events");
                    continue;
                }

                // Handle day/night transitions
                if (_lastDayTime.HasValue && _lastDayTime != status.DayTime)
                {
                    await NarrateTransition(status, status.DayTime
                        ? "El amanecer llega al mundo. Narra el inicio de un nuevo día de forma épica."
                        : "La noche cae sobre el mundo. Narra la llegada de la oscuridad de forma dramática.");
                }

                // Handle bloodmoon
                if (_lastBloodMoon.HasValue && _lastBloodMoon != status.BloodMoon)
                {
                    if (status.BloodMoon)
                        await NarrateTransition(status, "¡Una LUNA DE SANGRE aparece! Narra el evento aterrador.");
                    else
                        await NarrateTransition(status, "La luna de sangre se disipa. Narra el alivio del mundo.");
                }

                // Handle eclipse
                if (_lastEclipse.HasValue && _lastEclipse != status.Eclipse)
                {
                    if (status.Eclipse)
                        await NarrateTransition(status, "¡Un ECLIPSE oscurece el cielo! Narra la llegada de las sombras.");
                    else
                        await NarrateTransition(status, "El eclipse termina. Narra la luz retornando al mundo.");
                }

                // Ambient events
                if (DateTime.UtcNow >= _nextAmbientTime)
                {
                    await FireAmbientEvent(status);
                    _nextAmbientTime = DateTime.UtcNow.AddMinutes(
                        _config.AmbientIntervalMinutes + _random.Next(-_config.AmbientJitterMinutes, _config.AmbientJitterMinutes + 1));
                }

                // Boss check
                if (DateTime.UtcNow >= _nextBossCheckTime && playerCount >= _config.MinPlayersForBoss)
                {
                    await DecideBoss(status);
                    _nextBossCheckTime = DateTime.UtcNow.AddMinutes(
                        _config.BossCheckIntervalMinutes + _random.Next(-_config.BossCheckJitterMinutes, _config.BossCheckJitterMinutes + 1));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoEvent tick error");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private void DetectTransitions(TerrariaStatus status)
    {
        _lastDayTime = status.DayTime;
        _lastBloodMoon = status.BloodMoon;
        _lastEclipse = status.Eclipse;
    }

    private async Task NarrateTransition(TerrariaStatus status, string prompt)
    {
        try
        {
            _logger.LogInformation("Transition detected, narrating...");
            var narration = await _groq.GenerateEventNarrationAsync(prompt, status);
            await _tshock.BroadcastMessageAsync($"[Narrador] {narration}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to narrate transition");
        }
    }

    private async Task FireAmbientEvent(TerrariaStatus status)
    {
        try
        {
            var scenario = AmbientScenarios[_random.Next(AmbientScenarios.Length)];
            _logger.LogInformation("Ambient event: {Scenario}", scenario);
            var narration = await _groq.GenerateEventNarrationAsync(scenario, status);
            await _tshock.BroadcastMessageAsync($"[Narrador] {narration}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fire ambient event");
        }
    }

    private async Task DecideBoss(TerrariaStatus status)
    {
        try
        {
            var context = $"Hora: {(status.DayTime ? "día" : "noche")}. " +
                          $"Eventos: {(status.BloodMoon ? "luna de sangre " : "")}{(status.Eclipse ? "eclipse " : "")}. " +
                          $"Jugadores: {string.Join(", ", status.Players.Select(p => p.Name))}.";

            var prompt = $@"Contexto del mundo: {context}
Dificultad: Master.

¿Debería invocar un boss ahora? Considera la hora, los eventos activos y los jugadores.
Responde SOLO con este JSON:
{{""spawn"": true/false, ""boss"": ""nombre del boss o null"", ""reason"": ""por qué""}}

Bosses disponibles: KingSlime, EyeOfCthulhu, EaterOfWorlds, Skeletron, QueenBee, TheTwins, TheDestroyer, SkeletronPrime, Plantera, Golem, LunaticCultist, MoonLord";

            var narration = await _groq.GenerateEventNarrationAsync(prompt, status);

            // Parse the response
            var json = narration.Trim();
            if (json.StartsWith("```"))
                json = json.Replace("```json", "").Replace("```", "").Trim();

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var shouldSpawn = root.TryGetProperty("spawn", out var s) && s.GetBoolean();
            var boss = root.TryGetProperty("boss", out var b) ? b.GetString() : null;
            var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : "";

            if (shouldSpawn && !string.IsNullOrEmpty(boss))
            {
                _logger.LogInformation("Boss decision: SPAWN {Boss} — {Reason}", boss, reason);

                // Generate epic narration before spawning
                var epicNarration = await _groq.GenerateEventNarrationAsync(
                    $"¡Un {boss} aparece en el mundo! {reason} Narra la aparición de forma épica y dramática.",
                    status);

                await _tshock.SpawnBossAsync(boss);
                await _tshock.BroadcastMessageAsync($"[Narrador] {epicNarration}");
            }
            else
            {
                _logger.LogInformation("Boss decision: NO SPAWN — {Reason}", reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decide boss");
        }
    }
}
```

- [ ] **Step 2: Verify build compiles**

```bash
cd /home/roman/k8s-projects/terraria-agent/src/Terraria.Agent.Api
dotnet build --no-restore 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-agent/src/Terraria.Agent.Api/Services/AutoEventService.cs
git commit -m "feat: rewrite AutoEventService — game state tracking, transitions, ambient, boss AI"
```

---

### Task 5: Add AutoEvent env vars to K8s deployment

**Files:**
- Modify: `terraria-agent/k8s/deployment.yaml`

**Interfaces:**
- Consumes: `AutoEventConfig` keys from Task 1

- [ ] **Step 1: Add env vars to deployment.yaml**

Add these env vars to the container spec, after the existing `Groq__ApiKey` entry:

```yaml
            - name: AutoEvent__PollIntervalSeconds
              value: "30"
            - name: AutoEvent__AmbientIntervalMinutes
              value: "15"
            - name: AutoEvent__AmbientJitterMinutes
              value: "2"
            - name: AutoEvent__BossCheckIntervalMinutes
              value: "25"
            - name: AutoEvent__BossCheckJitterMinutes
              value: "5"
            - name: AutoEvent__MinPlayersForBoss
              value: "1"
```

- [ ] **Step 2: Commit**

```bash
cd /home/roman/k8s-projects
git add terraria-agent/k8s/deployment.yaml
git commit -m "feat: add AutoEvent env vars to K8s deployment"
```

---

### Task 6: Build Docker image and import to k3s

**Files:**
- Dockerfile (no changes, just build)

**Interfaces:**
- Consumes: All code from Tasks 1-4

- [ ] **Step 1: Build Docker image**

```bash
cd /home/roman/k8s-projects/terraria-agent
sudo docker build --no-cache -t terraria-agent:latest .
```

Expected: `Successfully built <hash>`

- [ ] **Step 2: Import image to k3s**

```bash
sudo docker save terraria-agent:latest | sudo k3s ctr images import -
```

Expected: `unpacking docker.io/library/terraria-agent:latest`

- [ ] **Step 3: Apply K8s manifests and rollout restart**

```bash
sudo k3s kubectl apply -f /home/roman/k8s-projects/terraria-agent/k8s/namespace.yaml
sudo k3s kubectl apply -f /home/roman/k8s-projects/terraria-agent/k8s/secret.yaml
sudo k3s kubectl apply -f /home/roman/k8s-projects/terraria-agent/k8s/deployment.yaml
sudo k3s kubectl apply -f /home/roman/k8s-projects/terraria-agent/k8s/service.yaml
sudo k3s kubectl rollout restart deployment/terraria-agent -n terraria
```

- [ ] **Step 4: Verify pod is Ready**

```bash
sudo k3s kubectl get pods -n terraria -l app=terraria-agent
```

Expected: `terraria-agent-xxxxx   1/1   Running`

- [ ] **Step 5: Check logs for AutoEventService startup**

```bash
sudo k3s kubectl logs -n terraria -l app=terraria-agent --tail=20
```

Expected: `AutoEventService started. Poll=30s, Ambient=15min, Boss=25min`

- [ ] **Step 6: Commit**

```bash
cd /home/roman/k8s-projects
git add -A
git commit -m "deploy: terraria-agent with auto events system"
```

---

### Task 7: End-to-end verification

**Files:**
- None (verification only)

- [ ] **Step 1: Scale up Terraria server**

```bash
sudo k3s kubectl scale statefulset terraria-server -n terraria --replicas=1
```

- [ ] **Step 2: Wait for server to be Ready (may take 5-10 min)**

```bash
sudo k3s kubectl get pods -n terraria -l app=terraria-server -w
```

Wait for `1/1 Running`.

- [ ] **Step 3: Verify agent can reach TShock**

```bash
sudo k3s kubectl exec -n terraria deploy/terraria-agent -- curl -s http://terraria-server:7878/v2/server/status?token=terraria-agent-secret-token-2024 | head -c 200
```

Expected: JSON with `dayTime`, `players`, etc.

- [ ] **Step 4: Verify AutoEventService is polling**

```bash
sudo k3s kubectl logs -n terraria -l app=terraria-agent --tail=30 | grep -i "autoevent\|server status\|players"
```

Expected: Log entries showing server status polling and player count.

- [ ] **Step 5: Update CONTEXT.md**

Mark items as complete and add session 7 notes.

- [ ] **Step 6: Final commit**

```bash
cd /home/roman/k8s-projects
git add CONTEXT.md
git commit -m "docs: update CONTEXT.md with auto events status"
```
