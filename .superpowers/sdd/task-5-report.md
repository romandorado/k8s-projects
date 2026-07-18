## Task 5: GroqService

**Status:** DONE

### What was implemented

Created `GroqService.cs` — an HTTP-based service that calls the Groq API for AI narration of Terraria game events.

**Key behaviors:**
- Calls `https://api.groq.com/openai/v1/chat/completions` with configurable endpoint, model, and API key from `IConfiguration`
- System prompt defines the narrator persona ("MundoSobrinos" Master world, Spanish, dramatic/fun tone)
- `GenerateNarrationAsync(string userMessage, string context)` appends dynamic context to the system prompt
- 200 max tokens, temperature 0.8
- Graceful error handling — returns fallback messages on failure
- Bearer token auth set per request via `DefaultRequestHeaders`

### Program.cs

Added `builder.Services.AddHttpClient<GroqService>();` — uses typed HttpClient pattern (matching existing `TShockClient`).

### Build verification

Build succeeded (0 errors, 0 warnings):
```
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 ~/.dotnet/dotnet build
-> Terraria.Agent.Api.dll
```

### Commit

Pending — user did not request a commit.

### Files changed

- **Created:** `terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs` (96 lines)
- **Modified:** `terraria-agent/src/Terraria.Agent.Api/Program.cs` (+1 line: service registration)

### Concerns

None. Implementation matches the task brief exactly.
