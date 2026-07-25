# Agent Intelligence Upgrade — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the Terraria agent from a dumb narrator that invents facts into an intelligent game assistant with persistent memory, real knowledge injection, and accurate responses.

**Architecture:** Three-layer upgrade: (1) SQLite chat history for persistent memory, (2) Knowledge injection service that feeds real game data into prompts, (3) Improved system prompts that force the model to use provided data instead of inventing.

**Tech Stack:** C# / .NET 10, SQLite (Microsoft.Data.Sqlite), existing Groq API integration

## Global Constraints

- .NET 10 runtime (existing in terraria-agent)
- Groq API with `llama-3.3-70b-versatile` model
- SQLite database at `/app/data/agent.db` (PersistentVolume)
- Must not break existing `/agente` commands or ChatBridge integration
- All data files in `Data/` directory as JSON
- Spanish language for all player-facing responses

---

### Task 1: Chat History Service (SQLite Persistence)

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Services/ChatHistory.cs`
- Modify: `terraria-agent/src/Terraria.Agent.Api/Program.cs`

**Interfaces:**
- Consumes: SQLite database connection
- Produces: `ChatHistory.SaveMessageAsync(player, role, message)`, `ChatHistory.GetHistoryAsync(player, limit)`, `ChatHistory.PruneOldMessagesAsync(maxPerPlayer)`

- [ ] **Step 1: Create ChatHistory.cs with SQLite setup**

```csharp
using Microsoft.Data.Sqlite;

namespace Terraria.Agent.Api.Services;

public class ChatHistory
{
    private readonly string _dbPath;
    private readonly ILogger<ChatHistory> _logger;

    public ChatHistory(IConfiguration config, ILogger<ChatHistory> logger)
    {
        _dbPath = config["Database:Path"] ?? "/app/data/agent.db";
        _logger = logger;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS chat_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                player TEXT NOT NULL,
                role TEXT NOT NULL,
                message TEXT NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_chat_player ON chat_history(player, created_at);";
        cmd.ExecuteNonQuery();
        _logger.LogInformation("Chat history database initialized at {Path}", _dbPath);
    }

    public async Task SaveMessageAsync(string player, string role, string message)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO chat_history (player, role, message) VALUES (@player, @role, @message)";
        cmd.Parameters.AddWithValue("@player", player);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@message", message);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(string Role, string Message, DateTime Time)>> GetHistoryAsync(string player, int limit = 20)
    {
        var history = new List<(string Role, string Message, DateTime Time)>();
        
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT role, message, created_at FROM chat_history 
            WHERE player = @player 
            ORDER BY created_at DESC 
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@player", player);
        cmd.Parameters.AddWithValue("@limit", limit);
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            history.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDateTime(2)
            ));
        }
        
        history.Reverse(); // oldest first
        return history;
    }

    public async Task PruneOldMessagesAsync(int maxPerPlayer = 500)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM chat_history WHERE id IN (
                SELECT id FROM chat_history 
                WHERE player = @player 
                ORDER BY created_at DESC 
                LIMIT -1 OFFSET @max
            )";
        cmd.Parameters.AddWithValue("@max", maxPerPlayer);
        
        // Run for each player
        var playersCmd = connection.CreateCommand();
        playersCmd.CommandText = "SELECT DISTINCT player FROM chat_history";
        using var reader = await playersCmd.ExecuteReaderAsync();
        
        var players = new List<string>();
        while (await reader.ReadAsync())
            players.Add(reader.GetString(0));
        
        foreach (var player in players)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@player", player);
            cmd.Parameters.AddWithValue("@max", maxPerPlayer);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
```

- [ ] **Step 2: Register ChatHistory in Program.cs**

Add to `Program.cs` after other service registrations:
```csharp
builder.Services.AddSingleton<ChatHistory>();
```

- [ ] **Step 3: Verify it compiles**

Run: `cd terraria-agent/src/Terraria.Agent.Api && dotnet build`
Expected: BUILD SUCCESSFUL

- [ ] **Step 4: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Services/ChatHistory.cs terraria-agent/src/Terraria.Agent.Api/Program.cs
git commit -m "feat(agent): add SQLite chat history persistence"
```

---

### Task 2: Knowledge Service (Data Injection)

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Services/KnowledgeService.cs`
- Modify: `terraria-agent/src/Terraria.Agent.Api/Program.cs`

**Interfaces:**
- Consumes: JSON data files from `Data/` directory
- Produces: `KnowledgeService.SearchCraftingAsync(query)`, `KnowledgeService.SearchBossAsync(query)`, `KnowledgeService.GetGameContext()`, `KnowledgeService.GetKnowledgeContext(query)`

- [ ] **Step 1: Create KnowledgeService.cs**

```csharp
using System.Text.Json;

namespace Terraria.Agent.Api.Services;

public class KnowledgeService
{
    private readonly ILogger<KnowledgeService> _logger;
    private readonly CraftingService _crafting;
    private readonly string _dataPath;
    
    private List<BossData> _bosses = new();
    private List<GameData> _gameData = new();

    public KnowledgeService(IConfiguration config, ILogger<KnowledgeService> logger, CraftingService crafting)
    {
        _logger = logger;
        _crafting = crafting;
        _dataPath = config["Data:Path"] ?? "Data";
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            var bossesPath = Path.Combine(_dataPath, "bosses.json");
            if (File.Exists(bossesPath))
            {
                var json = File.ReadAllText(bossesPath);
                _bosses = JsonSerializer.Deserialize<List<BossData>>(json) ?? new();
                _logger.LogInformation("Loaded {Count} boss entries", _bosses.Count);
            }

            var gamePath = Path.Combine(_dataPath, "gamedata.json");
            if (File.Exists(gamePath))
            {
                var json = File.ReadAllText(gamePath);
                _gameData = JsonSerializer.Deserialize<List<GameData>>(json) ?? new();
                _logger.LogInformation("Loaded {Count} game data entries", _gameData.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load knowledge data");
        }
    }

    public async Task<string?> SearchCraftingAsync(string query)
    {
        return await _crafting.Search(query);
    }

    public string? SearchBoss(string query)
    {
        var lower = query.ToLower();
        var match = _bosses.FirstOrDefault(b => 
            lower.Contains(b.Name.ToLower()) ||
            b.Aliases.Any(a => lower.Contains(a.ToLower())));
        
        return match?.ToContextString();
    }

    public string GetGameContext()
    {
        return "Mundo: MundoSobrinos (Master difficulty). Jugadores: verificar en vivo.";
    }

    public string GetKnowledgeContext(string query)
    {
        var context = new List<string>();
        
        // Search crafting
        var craftingResult = _crafting.Search(query).Result;
        if (!string.IsNullOrEmpty(craftingResult))
            context.Add($"CRAFTING:\n{craftingResult}");
        
        // Search boss
        var bossResult = SearchBoss(query);
        if (!string.IsNullOrEmpty(bossResult))
            context.Add($"BOSS:\n{bossResult}");
        
        // Search game data
        var lower = query.ToLower();
        var gameMatches = _gameData.Where(g => 
            lower.Contains(g.Topic.ToLower()) ||
            g.Keywords.Any(k => lower.Contains(k.ToLower())));
        foreach (var match in gameMatches.Take(2))
            context.Add($"{match.Topic.ToUpper()}:\n{match.Info}");
        
        return context.Count > 0 
            ? string.Join("\n\n", context) 
            : "";
    }
}

public class BossData
{
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public int Hp { get; set; }
    public int Damage { get; set; }
    public int Defense { get; set; }
    public string[] Drops { get; set; } = Array.Empty<string>();
    public string SpawnCondition { get; set; } = "";
    public string Tips { get; set; } = "";

    public string ToContextString()
    {
        return $"Boss: {Name}\nHP: {Hp}\nDaño: {Damage}\nDefensa: {Defense}\n" +
               $"Drops: {string.Join(", ", Drops)}\nInvocación: {SpawnCondition}\nTips: {Tips}";
    }
}

public class GameData
{
    public string Topic { get; set; } = "";
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string Info { get; set; } = "";
}
```

- [ ] **Step 2: Register KnowledgeService in Program.cs**

```csharp
builder.Services.AddSingleton<KnowledgeService>();
```

- [ ] **Step 3: Verify it compiles**

Run: `cd terraria-agent/src/Terraria.Agent.Api && dotnet build`
Expected: BUILD SUCCESSFUL

- [ ] **Step 4: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Services/KnowledgeService.cs
git commit -m "feat(agent): add knowledge service for data injection"
```

---

### Task 3: Boss Data JSON

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Data/bosses.json`

- [ ] **Step 1: Create bosses.json with all vanilla bosses**

```json
[
  {
    "Name": "King Slime",
    "Aliases": ["rey slime", "slime king", "slim"],
    "Hp": 2000,
    "Damage": 15,
    "Defense": 10,
    "Drops": ["Ninja Hood", "Royal Gel", "Slime Mount"],
    "SpawnCondition": "Usa Slime Crown en el Bosque, o déjalo caer un Slime Blue sobre un Gel de slime",
    "Tips": "El más fácil. Salta para evitar contacto. Usa plataforma para esquivar."
  },
  {
    "Name": "Eye of Cthulhu",
    "Aliases": ["ojo", "ojo de cthulhu", "eye", "ojo cthulhu"],
    "Hp": 2800,
    "Damage": 15,
    "Defense": 12,
    "Drops": ["Demonite", "Corrupt Seeds", "Suspicious Looking Eye"],
    "SpawnCondition": "Usa Suspicious Looking Eye en la noche, o mata 50 ojos",
    "Tips": "Fase 1: esquiva. Fase 2: CORRE, se mueve más rápido. Usa arena con platforms."
  },
  {
    "Name": "Eater of Worlds",
    "Aliases": ["gusano", "eater", "eater of worlds", "eater de mundos"],
    "Hp": 8400,
    "Damage": 22,
    "Defense": 14,
    "Drops": ["Demonite", "Shadow Scale", "Eater of Worlds Trophy"],
    "SpawnCondition": "Usa Worm Food en el Corruption, o rompe 3 Shadow Orbs",
    "Tips": "Apunta a las secciones del medio. UsaWeapons piercing ( javelins, spears). No dejes que te rodee."
  },
  {
    "Name": "Queen Bee",
    "Aliases": ["abeja", "queen bee", "reina abeja", "abeja reina"],
    "Hp": 32000,
    "Damage": 30,
    "Defense": 14,
    "Drops": ["Bee Gun", "Bee Keeper", "Honeyed Goggles", "Nectar"],
    "SpawnCondition": "Usa Abebee Hive en el Jungle, o rompe un Larva en la Abehive",
    "Tips": "Esquiva sus embestidas horizontales. Usa plataformas. En fase 2 se mueve más rápido."
  },
  {
    "Name": "Skeletron",
    "Aliases": ["esqueleto", "skeletron", "skele"],
    "Hp": 4400,
    "Damage": 32,
    "Defense": 10,
    "Drops": ["Skeletron Mask", "Book of Skulls", "Hardy Saddle"],
    "SpawnCondition": "Habla con el Old Man en la Dungeon en la noche, o usa Clothier Voodoo Doll",
    "Tips": "Apunta a la cabeza. Cuando la cabeza se separe, CORRE. Usa arena amplia."
  },
  {
    "Name": "Wall of Flesh",
    "Aliases": ["muro", "wall", "wall of flesh", "muro de carne"],
    "Hp": 80000,
    "Damage": 50,
    "Defense": 0,
    "Drops": ["Pwnhammer", "Emblem", "Gun", "Hallowed armor materials"],
    "SpawnCondition": "Lanza Guide Voodoo Doll al lava en la Underworld",
    "Tips": "Último boss pre-hardmode. Usa platform runway. Weapons piercing. No te detengas."
  },
  {
    "Name": "The Twins",
    "Aliases": ["twins", "gemelos", "retinazer", "spazmatism", "los gemelos"],
    "Hp": 20000,
    "Damage": 40,
    "Defense": 10,
    "Drops": ["Soul of Light", "Soul of Night", "Hallowed Bars"],
    "SpawnCondition": "Usa Mechanical Eye en la noche, o espera spawn aleatorio en hardmode",
    "Tips": "Mata a Spazmatism (verde) primero. Retinazer (rojo) es más fácil solo. Usa platform arena alta."
  },
  {
    "Name": "The Destroyer",
    "Aliases": ["destructor", "destroyer", "the destroyer", "el destructor"],
    "Hp": 80000,
    "Damage": 43,
    "Defense": 0,
    "Drops": ["Soul of Might", "Hallowed Bars", "Destroyer Trophy"],
    "SpawnCondition": "Usa Mechanical Worm en la noche, o espera spawn aleatorio",
    "Tips": "Weapons piercing (daña todas las secciones). Mata los probes que suelta. Arena con platforms."
  },
  {
    "Name": "Skeletron Prime",
    "Aliases": ["primo", "skeletron prime", "skele prime", "skeletron prime"],
    "Hp": 25000,
    "Damage": 35,
    "Defense": 24,
    "Drops": ["Soul of Fright", "Hallowed Bars", "Skeletron Prime Trophy"],
    "SpawnCondition": "Usa Mechanical Skull en la noche, o espera spawn aleatorio",
    "Tips": "Mata los brazos primero (Laser y Saw son los más peligrosos). Luego la cabeza."
  },
  {
    "Name": "Plantera",
    "Aliases": ["plantera", "plantera"],
    "Hp": 30000,
    "Damage": 50,
    "Defense": 18,
    "Drops": ["Temple Key", "Greater Healing Potion", "Plantera Trophy"],
    "SpawnCondition": "Rombe un Plantera's Bulb en el Jungle Hardmode",
    "Tips": "Arena circular en el Jungle. Fase 2: se mueve más rápido, usa dashes. Mushroom armor helps."
  },
  {
    "Name": "Golem",
    "Aliases": ["golem", "golem"],
    "Hp": 25000,
    "Damage": 60,
    "Defense": 24,
    "Drops": ["Golem Trophy", "Greater Healing Potion", "Beetle husk"],
    "SpawnCondition": "Usa Lihzahrd Power Cell en el Altar en la Temple",
    "Tips": "Mata las manos primero, luego la cabeza. Esquiva sus puños. Usa arena amplia."
  },
  {
    "Name": "Lunatic Cultist",
    "Aliases": ["cultista", "lunatic", "lunatic cultist", "cultista lunático"],
    "Hp": 30000,
    "Damage": 50,
    "Defense": 10,
    "Drops": ["Ancient Manipulator", "Lunar Fragments"],
    "SpawnCondition": "Mata los 4 cultistas en el altar del Dungeon después de Golem",
    "Tips": "Esquiva sus clones. Cuando se duplica, ataca al REAL (no al fantasma). Mueve mucho."
  },
  {
    "Name": "Moon Lord",
    "Aliases": ["moon lord", "moon", "lord", "señor", "señor luna", "moon lord"],
    "Hp": 150000,
    "Damage": 100,
    "Defense": 50,
    "Drops": ["Luminite", "Moon Lord Trophy", "Sdmm", "Terrarian", "Star Wrath"],
    "SpawnCondition": "Derrota al Lunatic Cultist 4 veces, o usa Celestial Sigil",
    "Tips": "El jefe final. Mata los ojos de las manos primero, luego la cabeza. Usa Rod of Discord. Arena amplia."
  }
]
```

- [ ] **Step 2: Verify JSON is valid**

Run: `python3 -c "import json; json.load(open('terraria-agent/src/Terraria.Agent.Api/Data/bosses.json')); print('Valid JSON')"`
Expected: `Valid JSON`

- [ ] **Step 3: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Data/bosses.json
git commit -m "feat(agent): add boss stats database"
```

---

### Task 4: Game Knowledge JSON

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Data/gamedata.json`

- [ ] **Step 1: Create gamedata.json with game mechanics**

```json
[
  {
    "Topic": "Biomas",
    "Keywords": ["bioma", "biome", "cave", "underground", "surface", "ocean", "jungle", "corruption", "crimson", "hallow", "desert", "snow", "mushroom", "space", "hell", "underworld"],
    "Info": "Biomas: Forest (surface), Underground/Caverns, Corruption/Crimson (evil), Hallow (hardmode), Desert, Snow/Tundra, Jungle, Ocean, Space, Hell/Underworld. Cada bioma tiene enemies y resources únicos."
  },
  {
    "Topic": "Crafting Stations",
    "Keywords": ["mesa", "anvil", "forge", "workbench", "crafting", "station", "estación", "horn", "furnace", "kiln", "loom", "tinkerer"],
    "Info": "Estaciones: Work Bench (básico), Furnace (lingotes), Anvil (armas), Mythril/Orichalcum Anvil (hardmode), Hardmode Forge, Tinkerer Workshop (combina accesorios), Loom (ropa), Demon/Crimson Altar (barras especiales)."
  },
  {
    "Topic": "NPCs",
    "Keywords": ["npc", "merchant", "guide", "nurse", "arms dealer", "dryad", "goblin tinkerer", "wizard", "tax collector", "angler"],
    "Info": "NPCs aparecen al cumplir condiciones. Guide: recetas. Nurse: cura. Merchant: items. Goblin Tinkerer: reforge. Dryad: purification. Angler: fishing quests. Tax Collector: money."
  },
  {
    "Topic": "Armour Sets",
    "Keywords": ["armadura", "armor", "set", "helmet", "breastplate", "legs", "plat", "mithril", "orichalcum", "chlorophyte", "turtle", "beetle", "shroomite", "spectre"],
    "Info": "Set bonuses importantes: Cobalt (+speed), Mythril (+damage), Orichalcum (+projectiles), Palladium (+regen), Titanium (+defense orbs), Adamantite (+damage). Endgame: Chlorophyte, Turtle, Shroomite, Spectre, Solar/Vortex/Nebula/Stardust."
  },
  {
    "Topic": "Accessories",
    "Keywords": ["accesorio", "accessory", "wing", "shield", "boots", "cloud", "balloon", "neptune", "frostspark", "terraspark", "ankh", "destroyer", "worm"],
    "Info": "Accesios key: Wings (flight), Shield of Cthulhu (dash), Terraspark Boots (speed+waterwalk), Ankh Shield (immunities), Destroyer Emblem (+damage+crit), Celestial Shell (stats). Reforge en Goblin Tinkerer."
  },
  {
    "Topic": "Events",
    "Keywords": ["evento", "event", "blood moon", "eclipse", "goblin", "pirate", "martian", "frost legion", "solar eclipse", "pumpkin", "frost"],
    "Info": "Eventos: Blood Moon (enemigos fuertes + fishing), Eclipse Solar (enemigos raros), Invasión Goblin, Invasión Pirates, Martian Madness, Frost Legion, Pumpkin Moon, Frost Moon. Cada uno da items exclusivos."
  },
  {
    "Topic": "Potions",
    "Keywords": ["potion", "pocima", "potion", "healing", "mana", "ironskin", "swiftness", "regeneration", "endurance", "lifeforce", "rage", "wrath"],
    "Info": "Potions importantes: Healing (restaura HP), Mana (restaura mana), Ironskin (+def), Swiftness (+speed), Regeneration (+regen), Endurance (-damage), Lifeforce (+max HP), Rage (+crit), Wrath (+damage). Brew en Placed Bottles."
  },
  {
    "Topic": "Tips Hardmode",
    "Keywords": ["hardmode", "hard", "post wall", "after wall", "consejo", "tips", "qué hacer", "ayuda", "help"],
    "Info": "Post-Wall of Flesh: 1) Break altars for ore, 2) Get first hardmode anvil, 3) Build arena for mechanical bosses, 4) Kill mechanical bosses for Hallowed bars, 5) Plantera → Golem → Lunatic Cultist → Moon Lord."
  }
]
```

- [ ] **Step 2: Verify JSON is valid**

Run: `python3 -c "import json; json.load(open('terraria-agent/src/Terraria.Agent.Api/Data/gamedata.json')); print('Valid JSON')"`
Expected: `Valid JSON`

- [ ] **Step 3: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Data/gamedata.json
git commit -m "feat(agent): add game knowledge database"
```

---

### Task 5: Update IntentParser (Prompt + Knowledge Injection)

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Services/IntentParser.cs`

**Interfaces:**
- Consumes: `KnowledgeService`, `ChatHistory`
- Produces: Improved `ParseAsync` with better prompts and knowledge context

- [ ] **Step 1: Update IntentParser constructor to accept new services**

Change constructor signature:
```csharp
public IntentParser(HttpClient httpClient, IConfiguration config, ILogger<IntentParser> logger, 
    CraftingService crafting, KnowledgeService knowledge, ChatHistory history)
{
    _httpClient = httpClient;
    _apiKey = config["Groq:ApiKey"]!;
    _model = config["Groq:Model"]!;
    _endpoint = config["Groq:Endpoint"]!;
    _logger = logger;
    _crafting = crafting;
    _knowledge = knowledge;
    _history = history;
}
```

Add fields:
```csharp
private readonly KnowledgeService _knowledge;
private readonly ChatHistory _history;
```

- [ ] **Step 2: Replace SystemPrompt constant**

```csharp
private const string SystemPrompt = @"Eres NARRADOR, el narrador épico del mundo 'MundoSobrinos' en Terraria (Master difficulty).
Tienes personalidad: dramático, gracioso, un poco exagerado, pero siempre útil. Juegas con sobrinos. Español casual.

REGLAS CRÍTICAS:
- USA SOLAMENTE la información de contexto proporcionada abajo
- Si no tienes datos de un item/boss/especial, di 'no tengo esa información'
- NUNCA inventes recetas, stats o mecánicas que no estén en los datos
- Si tienes datos, incluye: materiales exactos, estación de crafting, pasos
- Sé ÉPICO pero PRECISO

CONTEXTO DEL MUNDO:
{world_status}

DATOS RELEVANTES:
{knowledge_context}

Responde SOLO con este JSON:
{""respond"": true/false, ""action"": ""<comando>"", ""narration"": ""<respuesta>""}

COMANDOS DISPONIBLES (valores EXACTOS para 'action', o null si no hay comando):
Tiempo: ""time day"", ""time night"", ""time noon"", ""time dusk"", ""time midnight""
Eventos: ""worldevent bloodmoon"", ""worldevent eclipse"", ""worldevent fullmoon"", ""worldevent sandstorm"", ""worldevent meteor""
Invasiones: ""worldevent goblins"", ""worldevent pirates"", ""worldevent martians""
Bosses: ""spawnboss KingSlime"", ""spawnboss EyeOfCthulhu"", ""spawnboss EaterOfWorlds"", ""spawnboss Skeletron"", ""spawnboss QueenBee"", ""spawnboss TheTwins"", ""spawnboss TheDestroyer"", ""spawnboss SkeletronPrime"", ""spawnboss Plantera"", ""spawnboss Golem"", ""spawnboss LunaticCultist"", ""spawnboss MoonLord""
Clima: ""bridge rain on"", ""bridge rain off"", ""bridge rain heavy""

CUÁNDO RESPONDER (respond=true):
- Te llaman directamente: ""narrador"", ""agente"", ""oye""
- Piden una acción: ""lluvia"", ""invoca al ojo"", ""pon noche""
- Piden información: ""cómo craftear espada de fuego"", ""qué necesita el goblin tinkerer""
- Piden consejo: ""qué hacer ahora"", ""por dónde empiezo""
- Evento interesante ocurre en el mundo
- Te hacen una pregunta directa

CUÁNDO NO RESPONDER (respond=false):
- Conversación casual entre jugadores que no te involucra
- Mensajes repetidos o spam
- Frases muy cortas sin contexto: ""si"", ""ok"", ""jaja""
- Comandos de juego que no necesitan narración

REGLAS:
- action SOLO puede ser uno de los comandos de arriba, o null. NUNCA inventes comandos.
- Si te piden crafteo/recetas → action=null, responde con la receta completa usando los datos
- Si te piden invocar un boss → ejecuta spawnboss con el nombre exacto
- Si te piden cambiar hora/clima → ejecuta el comando correspondiente
- Para chistes, historias, conversación → action=null, solo narra con personalidad
- 'narration' SIEMPRE con texto. Sé ÉPICO, CREATIVO y CONVERSACIONAL.
- Si tienes DUDA sobre qué acción, pregunta en la narration (action=null)";
```

- [ ] **Step 3: Update ParseAsync to use knowledge and history**

Replace the crafting context section with:
```csharp
// Get chat history
var historyMessages = await _history.GetHistoryAsync(chatEvent.Player ?? "unknown", 20);

// Build history context
var historyContext = "";
if (historyMessages.Count > 0)
{
    var recent = historyMessages.TakeLast(10).ToList();
    historyContext = "\n\nHISTORIAL RECIENTE:\n" + 
        string.Join("\n", recent.Select(m => $"{m.Role}: {m.Message}"));
}

// Get knowledge context
var knowledgeContext = _knowledge.GetKnowledgeContext(chatEvent.Text);

// Build world status
var worldStatus = _knowledge.GetGameContext();

var systemMessage = SystemPrompt
    .Replace("{world_status}", worldStatus)
    .Replace("{knowledge_context}", knowledgeContext);

// Add history to system prompt if available
if (!string.IsNullOrEmpty(historyContext))
    systemMessage += historyContext;
```

- [ ] **Step 4: Update max_tokens and temperature**

Change the request object:
```csharp
var request = new
{
    model = _model,
    messages = apiMessages.ToArray(),
    max_tokens = 500,
    temperature = 0.65
};
```

- [ ] **Step 5: Save messages to history**

After processing, add to history:
```csharp
// Save to history
await _history.SaveMessageAsync(chatEvent.Player ?? "unknown", "user", chatEvent.Text);
if (result != null)
    await _history.SaveMessageAsync(chatEvent.Player ?? "unknown", "assistant", result.Narration);
```

- [ ] **Step 6: Verify it compiles**

Run: `cd terraria-agent/src/Terraria.Agent.Api && dotnet build`
Expected: BUILD SUCCESSFUL

- [ ] **Step 7: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Services/IntentParser.cs
git commit -m "feat(agent): improve IntentParser with knowledge injection and history"
```

---

### Task 6: Update GroqService (Prompt + Tokens)

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs`

- [ ] **Step 1: Update SystemPrompt**

```csharp
private const string SystemPrompt = @"Eres NARRADOR del mundo 'MundoSobrinos' en Terraria (Master difficulty).
Personalidad: dramático, gracioso, exagerado. Español casual.

REGLAS:
- USA la información de contexto proporcionada
- Si no tienes datos, di 'no tengo esa información'
- NUNCA inventes stats o mecánicas
- Sé ÉPICO y PRECISO. Máximo 500 tokens.";
```

- [ ] **Step 2: Update max_tokens**

```csharp
var request = new
{
    model = _model,
    messages = new[]
    {
        new { role = "system", content = systemMessage },
        new { role = "user", content = userMessage }
    },
    max_tokens = 500,
    temperature = 0.7
};
```

- [ ] **Step 3: Verify it compiles**

Run: `cd terraria-agent/src/Terraria.Agent.Api && dotnet build`
Expected: BUILD SUCCESSFUL

- [ ] **Step 4: Commit**

```bash
git add terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs
git commit -m "feat(agent): improve GroqService prompt and token limits"
```

---

### Task 7: Update Program.cs and Dockerfile

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Program.cs`
- Modify: `terraria-agent/Dockerfile`

- [ ] **Step 1: Ensure all services are registered in Program.cs**

Verify `Program.cs` has:
```csharp
builder.Services.AddSingleton<ChatHistory>();
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddSingleton<CraftingService>();
builder.Services.AddSingleton<IntentParser>();
builder.Services.AddSingleton<GroqService>();
builder.Services.AddSingleton<TShockClient>();
builder.Services.AddSingleton<CommandParser>();
```

- [ ] **Step 2: Add Microsoft.Data.Sqlite to project**

Run: `cd terraria-agent/src/Terraria.Agent.Api && dotnet add package Microsoft.Data.Sqlite`
Expected: Package added successfully

- [ ] **Step 3: Update Dockerfile to copy Data files**

Ensure Dockerfile has:
```dockerfile
COPY --from=build /app/Data ./Data
```

- [ ] **Step 4: Add volume mount for database in deployment.yaml**

Add to container volumeMounts:
```yaml
- name: agent-data
  mountPath: /app/data
```

Add to volumes:
```yaml
- name: agent-data
  emptyDir: {}
```

- [ ] **Step 5: Verify it compiles**

Run: `cd terraria-agent/src/Terraria.Agent.Api && dotnet build`
Expected: BUILD SUCCESSFUL

- [ ] **Step 6: Commit**

```bash
git add terraria-agent/
git commit -m "feat(agent): integrate all services and add SQLite dependency"
```

---

### Task 8: Build, Import, and Test

**Files:**
- None (build and test only)

- [ ] **Step 1: Build Docker image locally**

Run: `cd terraria-agent && docker build -t terraria-agent:latest .`
Expected: Image builds successfully

- [ ] **Step 2: Import to k3s**

Run: `docker save terraria-agent:latest | sudo k3s ctr images import -`
Expected: Image imported

- [ ] **Step 3: Restart agent deployment**

Run: `sudo k3s kubectl rollout restart deployment terraria-agent -n terraria`
Expected: New pod starts

- [ ] **Step 4: Wait for pod ready**

Run: `sudo k3s kubectl get pods -n terraria -l app=terraria-agent -w`
Expected: Pod becomes 1/1 Ready

- [ ] **Step 5: Test crafting question**

Run from curl-test pod:
```bash
curl -s -X POST http://terraria-agent:8080/api/chat \
  -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"player":"Tester","text":"¿Cómo se fabrica Excalibur?","eventType":"chat"}'
```

Check agent logs for:
- Real crafting data injected (Hallowed Bars, Mythril Anvil)
- Response contains accurate recipe

- [ ] **Step 6: Test boss question**

Run:
```bash
curl -s -X POST http://terraria-agent:8080/api/chat \
  -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"player":"Tester","text":"¿Cuántos HP tiene Moon Lord?","eventType":"chat"}'
```

Check logs for: 150000 HP (accurate data)

- [ ] **Step 7: Test memory persistence**

Ask a question, wait, ask a follow-up:
```bash
curl ... -d '{"player":"Tester","text":"Estoy en hardmode","eventType":"chat"}'
sleep 5
curl ... -d '{"player":"Tester","text":"¿Qué boss debo matar ahora?","eventType":"chat"}'
```

Check: Agent remembers "hardmode" context

- [ ] **Step 8: Commit all changes**

```bash
git add -A
git commit -m "feat(agent): complete intelligence upgrade - SQLite memory + knowledge DBs + improved prompts"
```

---

## Verification Checklist

After all tasks complete, verify:

1. **Crafting accuracy**: "¿Cómo se fabrica Excalibur?" → "12 Hallowed Bars en Mythril/Orichalcum Anvil"
2. **Boss stats**: "¿Cuántos HP tiene Moon Lord?" → "150000 HP"
3. **Memory**: Ask 5 questions, verify agent remembers context
4. **Narration**: Responses still have personality and epic tone
5. **Commands**: `/agente invocar`, `/agente hora`, etc. still work
6. **No regressions**: ChatBridge plugin still functions
7. **Builds**: Docker image builds and imports successfully
