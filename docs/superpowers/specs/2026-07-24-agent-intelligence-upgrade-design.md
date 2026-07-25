# Agent Intelligence Upgrade — Design Spec

## Problem

The Terraria agent gives inaccurate, truncated responses:

1. **System prompt lies** — says "Conoces TODAS las recetas" but doesn't inject actual data
2. **`max_tokens: 200`** — responses get cut off mid-sentence
3. **`MaxHistory: 8`** — loses conversation context after 8 messages
4. **No real knowledge injection** — model invents crafting recipes instead of using DB
5. **Temperature 0.75** — too high for factual accuracy

## Goal

A narrator + game assistant that gives **accurate data with personality**. When a player asks "how to craft Excalibur", they get the real recipe (12 Hallowed Bars, Mythril/Orichalcum Anvil) delivered with epic narration.

## Design

### 1. Persistent Chat History (SQLite)

**New table: `chat_history`**

```sql
CREATE TABLE IF NOT EXISTS chat_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    player TEXT NOT NULL,
    role TEXT NOT NULL,       -- 'user' or 'assistant'
    message TEXT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX idx_chat_player ON chat_history(player, created_at);
```

**Behavior:**
- Every incoming message → save to DB (role='user')
- Every response → save to DB (role='assistant')
- On new message → load last 20 messages for that player as context
- Old messages (>500) are pruned periodically
- New player (no history) → context is empty, agent starts fresh

**File:** `Services/ChatHistory.cs` (new)

### 2. Knowledge Injection System

Three data sources, injected as context into the system prompt:

#### a) Crafting DB (existing, needs improvement)
- `crafting.json` — 182 vanilla items
- WikiService fallback — queries `terraria.wiki.gg` for unknown items
- **Change:** Always inject relevant crafting data when player mentions items/materials
- **Change:** Cache Wiki results with TTL (not just forever)

#### b) Boss/Stats DB (new)
- **File:** `Data/bosses.json`
- Content: HP, damage, defense, drops, spawn conditions, attack patterns
- Covers all 15+ bosses and events
- Injected when player asks about bosses or combat

#### c) Game Knowledge DB (new)
- **File:** `Data/gamedata.json`
- Content: Biomes, NPCs, mechanics, events, tips
- Injected contextually based on conversation topic

**File:** `Services/KnowledgeService.cs` (new, replaces CraftingService for data injection)

### 3. Improved System Prompt

Replace the current prompt with one that:
- Instructs the model to USE provided data, not invent
- Says "Si no tienes datos proporcionados, di que no sabes"
- Separates factual from narrative instructions
- Gives the model the actual data in the prompt context

**Current prompt (IntentParser.cs):**
```
Eres NARRADOR... Conoces TODAS las recetas... Responde SOLO con JSON...
```

**New prompt structure:**
```
Eres NARRADOR del mundo 'MundoSobrinos' en Terraria Master difficulty.
Personalidad: dramático, gracioso, exagerado, español casual.

REGLAS CRÍTICAS:
- Usa SOLO la información de contexto proporcionada abajo
- Si no tienes datos de un item/boss, di "no tengo esa información"
- NUNCA inventes recetas, stats o mecánicas
- Si tienes datos, incluye: materiales exactos, estación de crafting, pasos
- Sé ÉPICO pero PRECISO

CONTEXTO DEL MUNDO:
{world_status}

DATOS RELEVANTES:
{knowledge_context}
```

### 4. Intent Parser Improvements

**Changes to `IntentParser.cs`:**

- `max_tokens`: 200 → 500
- `temperature`: 0.75 → 0.65
- Context injection: Always include world status + relevant knowledge
- Knowledge search: Use `KnowledgeService` instead of just `CraftingService`

### 5. GroqService Improvements

**Changes to `GroqService.cs`:**

- `max_tokens`: 200 → 500 (for /agente commands)
- Same system prompt improvements
- Include world context in all requests

### 6. Data Files

#### `Data/bosses.json`
```json
{
  "bosses": [
    {
      "name": "King Slime",
      "npcId": 50,
      "hp": 2000,
      "damage": 15,
      "defense": 10,
      "drops": ["Ninja Hood", "Royal Gel"],
      "spawnCondition": "Use Gel in Forest biome / Slime Crown",
      "tips": "Easiest boss. Jump to avoid contact."
    },
    ...
  ]
}
```

#### `Data/gamedata.json`
```json
{
  "biomes": [...],
  "npcs": [...],
  "mechanics": [...],
  "events": [...]
}
```

## Files to Modify

| File | Change |
|------|--------|
| `Services/ChatHistory.cs` | **NEW** — SQLite chat persistence |
| `Services/KnowledgeService.cs` | **NEW** — Unified knowledge injection |
| `Data/bosses.json` | **NEW** — Boss stats database |
| `Data/gamedata.json` | **NEW** — Game knowledge database |
| `Services/IntentParser.cs` | Update prompt, increase tokens, use KnowledgeService |
| `Services/GroqService.cs` | Update prompt, increase tokens |
| `Controllers/ChatController.cs` | Use ChatHistory for context |
| `Program.cs` | Register new services |

## Trade-offs

| Approach | Pros | Cons |
|----------|------|------|
| **SQLite memory + Knowledge DBs** | Accurate, persistent, full context | More code, DB maintenance |
| **Just increase MaxHistory** | Simple, no DB | Limited context, no world state |
| **Two models (70b + 8b)** | Best of both | More complexity, Groq config |
| **Keep current + fix prompt** | Minimal changes | Still limited by model knowledge |

**Chosen:** SQLite memory + Knowledge DBs (most impactful, aligns with user goals)

## Verification

1. Test crafting questions: "¿Cómo se fabrica Excalibur?" → Must return real recipe
2. Test boss knowledge: "¿Cuántos HP tiene Moon Lord?" → Must return accurate stats
3. Test conversation memory: Ask 5 questions, verify agent remembers context
4. Test narration quality: Still epic and personality-driven
5. Test command execution: `/agente invocar` still works
