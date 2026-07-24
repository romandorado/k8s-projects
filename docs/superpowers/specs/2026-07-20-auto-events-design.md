# Auto Events System - Design Spec

## Overview

Redesign the `AutoEventService` to be context-aware, hybrid (cron + reactive), and AI-driven for boss spawning. The service polls TShock for real game state, narrates transitions when players are online, and lets Groq decide when/how to spawn bosses.

## Goals

1. **No wasted Groq calls**: Only call Groq when players are online
2. **Context-aware narration**: React to real game state changes (day/night, weather)
3. **Ambient atmosphere**: Periodic flavor events for immersion
4. **AI-driven boss spawns**: Groq decides which boss and when, based on game context
5. **Configurable frequencies**: All intervals settable via `appsettings.json`

## Architecture

```
AutoEventService (BackgroundService)
├── Game State Tracker (poll TShock every 30s)
│   ├── Detects transitions: day→night, night→day, weather changes
│   ├── Tracks online players count
│   └── Always active (even with 0 players)
├── Event Dispatcher (only when players > 0)
│   ├── Transition Narration → Groq + TShock broadcast
│   ├── Ambient Events (cron 15-20 min) → Groq
│   └── Boss Decision (cron 25 min) → Groq + TShock spawnboss
└── Skip Logic
    └── players == 0: skip all Groq calls, log state changes only
```

## Components

### 1. Game State Tracker

Polls `/v2/server/status` every 30 seconds. Maintains a `GameState` object:

```csharp
public class GameState
{
    public bool DayTime { get; set; }
    public bool BloodMoon { get; set; }
    public bool Eclipse { get; set; }
    public int PlayerCount { get; set; }
    public List<string> PlayerNames { get; set; } = new();
    public DateTime LastPollTime { get; set; }
}
```

**Transition detection**: Compare current state with previous state. A "transition" is when `DayTime`, `BloodMoon`, or `Eclipse` changes value.

### 2. Transition Narration

When a transition is detected AND players > 0:

| Transition | Narration Prompt | Optional TShock Command |
|------------|-----------------|------------------------|
| day→night | "El sol se oculta, la noche llega..." | — |
| night→day | "El amanecer ilumina el mundo..." | — |
| bloodmoon start | "La luna sangrienta aparece..." | — |
| bloodmoon end | "La luna de sangre se disipa..." | — |
| eclipse start | "Un eclipse oscurece el cielo..." | — |
| eclipse end | "La luz retorna tras el eclipse..." | — |

The narration is sent to Groq with the full context (players, time, weather) to generate a unique message each time.

### 3. Ambient Events (Cron)

Every 15-20 minutes (randomized ±2 min to avoid predictability), if players > 0:

- Pick a random ambient scenario from a curated list
- Send to Groq with context for narration
- Broadcast via TShock

Scenarios include: wind picking up, distant thunder, fireflies at night, birds at dawn, mysterious sounds, etc.

### 4. Boss Decision Engine (AI-driven)

Every 25 minutes (randomized), if players > 0:

**Input to Groq:**
```
Contexto del mundo:
- Hora: día/noche
- Eventos activos: bloodmoon, eclipse, etc.
- Jugadores online: [nombres]
- Tiempo desde último boss: X min
- Dificultad: Master

¿Debería invocar un boss ahora? Si sí, ¿cuál?
Responde JSON: {"spawn": true/false, "boss": "nombre", "reason": "por qué"}
```

**Groq decides:**
- Whether to spawn (might say no if context doesn't fit)
- Which boss (based on progression, time of day, events)
- Provides reasoning for the narration

**If spawn = true:**
- Execute `spawnboss <bossName>` via TShock plugin
- Broadcast epic narration about the boss arrival

**Boss pool (appropriate for Master difficulty):**
- Early game: KingSlime, EyeOfCthulhu
- Mid game: EaterOfWorlds, Skeletron, QueenBee
- Late game: TheTwins, TheDestroyer, SkeletronPrime, Plantera, Golem
- Endgame: LunaticCultist, MoonLord

The AI should consider game progression (if players have defeated certain bosses, it won't re-spawn early ones).

### 5. Skip Logic

```csharp
// Every tick:
var status = await PollServer();
var playerCount = status.OnlinePlayers.Count;

// Tracker always runs
UpdateGameState(status);

// Skip if no players
if (playerCount == 0)
{
    _logger.LogDebug("No players online, skipping events");
    return;
}

// Proceed with transitions, ambient, boss logic...
```

## Configuration

Add to `appsettings.json`:

```json
{
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

All values overridable via environment variables in K8s deployment.

## Data Flow

```
TShock Server (7878)
    ↓ GET /v2/server/status
AutoEventService
    ↓ (if players > 0)
    ├── Transition detected → Groq API → TShock broadcast
    ├── Ambient timer fired → Groq API → TShock broadcast
    └── Boss timer fired → Groq API (decision) → TShock spawnboss + broadcast
```

## Error Handling

- **TShock unreachable**: Log error, skip this tick, retry next poll
- **Groq rate limit**: Log warning, skip event, continue next interval
- **Groq returns spawn=false**: Log info, no action taken
- **Invalid boss name from Groq**: Fallback to EyeOfCthulhu, log warning

## Files Modified

| File | Change |
|------|--------|
| `Services/AutoEventService.cs` | Complete rewrite: game state tracking, transitions, ambient, boss logic |
| `Services/TShockClient.cs` | Add `GetPlayersAsync()` method, `SpawnBossAsync()` method |
| `Services/GroqService.cs` | Add `GenerateEventNarrationAsync()` with context-aware prompts |
| `appsettings.json` | Add `AutoEvent` config section |
| `k8s/deployment.yaml` | Add `AutoEvent__*` env vars |
| `Program.cs` | No change (AutoEventService already registered) |

## Testing

1. **Unit**: Game state transition detection, skip logic (0 players)
2. **Integration**: Poll TShock, detect day→night transition
3. **E2E**: Start server, join with player, verify narration appears in-game
4. **Manual**: Wait for ambient event, verify Groq narration
5. **Boss**: Wait for boss check with active players, verify boss spawns

## Out of Scope

- Webhook-based event detection (polling is sufficient for this scale)
- Persistent event history (in-memory state is enough)
- Player-triggered events (handled by existing `/agente` commands)
- Multiple concurrent events (one at a time to avoid chaos)
