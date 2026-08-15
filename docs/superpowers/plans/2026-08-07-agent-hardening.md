# Agent Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Blindar el agente de Terraria: validar todas las acciones de Groq antes de ejecutarlas, conocer los jugadores online, y garantizar que nunca se quede callado ni narre falsos éxitos.

**Architecture:** Se añaden tres servicios .NET nuevos: `ActionValidator` (whitelist de comandos por familia + normalización), `OnlinePlayersService` (cache corta de jugadores online para inyección en prompt y guardia de ejecución) y `GroqRateLimiter` (token bucket compartido). Se refuerza el prompt de `IntentParser`, se añade backoff exponencial en 429 y narración por defecto. `ChatController` valida toda acción de Groq (Ruta 3) antes de ejecutarla.

**Tech Stack:** .NET 10 (C#), xunit (nuevo proyecto de tests), Groq API, TShock REST API, Docker/k3s.

## Global Constraints

- **Build**: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release`
- **Unit tests**: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Terraria.Agent.Api.Tests/Terraria.Agent.Api.Tests.csproj -c Release`
- **Estado del working tree**: `ChatController.cs` ya contiene el give-fix sin commitear (`HandleGiveAsync`, `GetGiveRequest`, `GiveItemAliases`, `GiveTriggerPrefixes`, el record privado `GiveRequest`). NO recrear estos; reutilizarlos.
- **REGLA 2**: desplegar SIEMPRE local y remoto con el mismo tag de imagen. Tag de este trabajo: `terraria-agent:v4-hardening`.
- **REGLA 3**: rebuild Docker con `--no-cache`.
- **REGLA 1**: guardar el servidor (`/save`) antes de reiniciarlo.
- Sin dependencias nuevas en la app de producción (solo el proyecto de tests añade xunit + `Microsoft.Extensions.Configuration*`).
- No añadir comentarios en el código nuevo; los strings de narración/prompt son datos de runtime, no comentarios.
- Commit por tarea en estilo del repo (`feat(agent): ...`, `fix(agent): ...`, `test(agent): ...`).

---

### Task 1: Proyecto de tests + `ActionValidator` (TDD)

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api.Tests/Terraria.Agent.Api.Tests.csproj`
- Create: `terraria-agent/src/Terraria.Agent.Api.Tests/ActionValidatorTests.cs`
- Create: `terraria-agent/src/Terraria.Agent.Api/Services/ActionValidator.cs`
- Test: `terraria-agent/src/Terraria.Agent.Api.Tests/ActionValidatorTests.cs`

**Interfaces:**
- Produces: `ActionValidator` (class, ctor sin parámetros) con método `ActionValidation Validate(string? action)`.
- Produces: `ActionValidation` (record/class) con propiedades `bool IsValid`, `string? Action`, `string? Reason`, `string? TargetPlayer`, `string? Item`, `string? GiveTarget`, `int Quantity` (default 1).

- [ ] **Step 1: Crear el proyecto de tests**

Create `terraria-agent/src/Terraria.Agent.Api.Tests/Terraria.Agent.Api.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Memory" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Terraria.Agent.Api\Terraria.Agent.Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Escribir los tests (fallarán — `ActionValidator` no existe)**

Create `terraria-agent/src/Terraria.Agent.Api.Tests/ActionValidatorTests.cs`:

```csharp
using Terraria.Agent.Api.Services;
using Xunit;

namespace Terraria.Agent.Api.Tests;

public class ActionValidatorTests
{
    private readonly ActionValidator _validator = new();

    [Theory]
    [InlineData("spawnboss EyeOfCthulhu", true)]
    [InlineData("spawnboss eye of cthulhu", true)]
    [InlineData("spawnboss The Twins", true)]
    [InlineData("spawnboss muralla de carne", true)]
    [InlineData("spawnboss Medusa", false)]
    [InlineData("spawnboss", false)]
    public void SpawnBoss_Validates(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Fact]
    public void SpawnBoss_NormalizesSpanishAliasToCanonicalName()
    {
        var result = _validator.Validate("spawnboss muralla de carne");
        Assert.True(result.IsValid);
        Assert.Equal("spawnboss WallOfFlesh", result.Action);
    }

    [Fact]
    public void SpawnBoss_NormalizesCanonicalNameCaseInsensitively()
    {
        var result = _validator.Validate("spawnboss eye of cthulhu");
        Assert.True(result.IsValid);
        Assert.Equal("spawnboss EyeOfCthulhu", result.Action);
    }

    [Fact]
    public void SpawnBoss_RejectsUnknownBoss()
    {
        var result = _validator.Validate("spawnboss Medusa");
        Assert.False(result.IsValid);
        Assert.Contains("jefe", result.Reason);
    }

    [Fact]
    public void Give_WithoutArgumentsIsRejected()
    {
        Assert.False(_validator.Validate("give").IsValid);
    }

    [Fact]
    public void Give_WithQuotedItemParsesTargetAndQuantity()
    {
        var result = _validator.Validate("give \"Iron Bar\" Testeador1 20");
        Assert.True(result.IsValid);
        Assert.Equal("Iron Bar", result.Item);
        Assert.Equal("Testeador1", result.GiveTarget);
        Assert.Equal(20, result.Quantity);
        Assert.Equal("Testeador1", result.TargetPlayer);
        Assert.Equal("give \"Iron Bar\" Testeador1 20", result.Action);
    }

    [Fact]
    public void Give_WithoutQuotesParsesItemAndTarget()
    {
        var result = _validator.Validate("give Iron Bar Testeador1 20");
        Assert.True(result.IsValid);
        Assert.Equal("Iron Bar", result.Item);
        Assert.Equal("Testeador1", result.GiveTarget);
        Assert.Equal(20, result.Quantity);
    }

    [Fact]
    public void Give_WithQuotedItemAndMultiWordTargetKeepsTargetIntact()
    {
        var result = _validator.Validate("give \"Life Fruit\" hor net");
        Assert.True(result.IsValid);
        Assert.Equal("Life Fruit", result.Item);
        Assert.Equal("hor net", result.GiveTarget);
    }

    [Theory]
    [InlineData("time 10", true)]
    [InlineData("time 22", true)]
    [InlineData("time 25", false)]
    [InlineData("time night", true)]
    [InlineData("time midnight", true)]
    [InlineData("time 10:30", false)]
    public void Time_AcceptsHourOrMoment(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Theory]
    [InlineData("worldevent sandstorm", true)]
    [InlineData("worldevent bloodmoon", true)]
    [InlineData("worldevent lluvia", false)]
    [InlineData("worldevent", false)]
    public void WorldEvent_AcceptsKnownEvents(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Theory]
    [InlineData("bridge rain on", true)]
    [InlineData("bridge rain heavy", true)]
    [InlineData("bridge rain maybe", false)]
    [InlineData("bridge slime rain off", true)]
    [InlineData("bridge slime rain", false)]
    public void Bridge_AcceptsKnownSubcommands(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Fact]
    public void Buff_IsRejected()
    {
        Assert.False(_validator.Validate("buff").IsValid);
    }

    [Theory]
    [InlineData("heal", null)]
    [InlineData("heal Sobrino2", "Sobrino2")]
    [InlineData("maxhp Testeador1", "Testeador1")]
    public void OptionalPlayer_FamiliesExposeTarget(string action, string? expected)
    {
        var result = _validator.Validate(action);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.TargetPlayer);
    }

    [Theory]
    [InlineData("tp", false)]
    [InlineData("tp Sobrino2", true)]
    [InlineData("kill", false)]
    [InlineData("kill Sobrino2", true)]
    [InlineData("kick Sobrino2 razon", true)]
    public void RequiredPlayer_FamiliesRequireTarget(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Fact]
    public void Hardmode_AndSave_AreValid()
    {
        Assert.True(_validator.Validate("hardmode").IsValid);
        Assert.True(_validator.Validate("save").IsValid);
        Assert.False(_validator.Validate("hardmode extra").IsValid);
    }

    [Theory]
    [InlineData("spawnmob zombie", "spawnmob zombie 1")]
    [InlineData("spawnmob zombie 10", "spawnmob zombie 10")]
    [InlineData("spawnmob Giant Bat 5", "spawnmob Giant Bat 5")]
    public void SpawnMob_ParsesMobAndOptionalQuantity(string action, string expected)
    {
        var result = _validator.Validate(action);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Action);
    }

    [Theory]
    [InlineData("maxspawns 10", true)]
    [InlineData("spawnrate 5", true)]
    [InlineData("maxspawns 0", false)]
    [InlineData("spawnrate xyz", false)]
    public void Numeric_FamiliesRequireNumber(string action, bool expected)
    {
        Assert.Equal(expected, _validator.Validate(action).IsValid);
    }

    [Fact]
    public void UnknownVerb_IsRejected()
    {
        Assert.False(_validator.Validate("teleportalo todo").IsValid);
    }

    [Fact]
    public void NullOrEmpty_IsRejected()
    {
        Assert.False(_validator.Validate(null!).IsValid);
        Assert.False(_validator.Validate("").IsValid);
    }
}
```

- [ ] **Step 3: Ejecutar los tests para verificar que fallan**

Run: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Terraria.Agent.Api.Tests/Terraria.Agent.Api.Tests.csproj -c Release`
Expected: FAIL — compilación con `CS0246` ("type or namespace 'ActionValidator' could not be found"). También restaura el proyecto de tests (primera vez baja xunit).

- [ ] **Step 4: Escribir la implementación mínima completa**

Create `terraria-agent/src/Terraria.Agent.Api/Services/ActionValidator.cs`:

```csharp
namespace Terraria.Agent.Api.Services;

public sealed class ActionValidation
{
    public bool IsValid { get; init; }
    public string? Action { get; init; }
    public string? Reason { get; init; }
    public string? TargetPlayer { get; init; }
    public string? Item { get; init; }
    public string? GiveTarget { get; init; }
    public int Quantity { get; init; } = 1;
}

public class ActionValidator
{
    private static readonly HashSet<string> TimeNames = new(StringComparer.OrdinalIgnoreCase)
        { "day", "night", "noon", "dusk", "midnight" };

    private static readonly HashSet<string> RainModes = new(StringComparer.OrdinalIgnoreCase)
        { "on", "off", "heavy" };

    private static readonly HashSet<string> SlimeRainModes = new(StringComparer.OrdinalIgnoreCase)
        { "on", "off" };

    private static readonly HashSet<string> WorldEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "bloodmoon", "eclipse", "fullmoon", "sandstorm", "meteor", "lanternsnight",
        "meteorshower", "coinrain", "star", "halloween", "xmas", "goblins", "pirates", "martians"
    };

    private static readonly HashSet<string> NoArgCommands = new(StringComparer.OrdinalIgnoreCase)
        { "hardmode", "home", "spawn", "setspawn", "settle", "butcher", "save" };

    private static readonly Dictionary<string, string> BossAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kingslime"] = "KingSlime",
        ["eyeofcthulhu"] = "EyeOfCthulhu",
        ["ojo"] = "EyeOfCthulhu",
        ["eaterofworlds"] = "EaterOfWorlds",
        ["gusano"] = "EaterOfWorlds",
        ["skeletron"] = "Skeletron",
        ["esqueleto"] = "Skeletron",
        ["queenbee"] = "QueenBee",
        ["abeja"] = "QueenBee",
        ["thetwins"] = "TheTwins",
        ["gemelos"] = "TheTwins",
        ["thedestroyer"] = "TheDestroyer",
        ["destructor"] = "TheDestroyer",
        ["skeletronprime"] = "SkeletronPrime",
        ["primo"] = "SkeletronPrime",
        ["plantera"] = "Plantera",
        ["golem"] = "Golem",
        ["lunaticcultist"] = "LunaticCultist",
        ["moonlord"] = "MoonLord",
        ["wallofflesh"] = "WallOfFlesh",
        ["muralladecarne"] = "WallOfFlesh",
        ["pareddecarne"] = "WallOfFlesh",
        ["murodecarne"] = "WallOfFlesh",
        ["wof"] = "WallOfFlesh"
    };

    public ActionValidation Validate(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return Invalid(null, "el comando está vacío");

        var parts = action.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var verb = parts[0].TrimStart('/').ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        switch (verb)
        {
            case "time":
                return ValidateTime(args);
            case "bridge":
                return ValidateBridge(args);
            case "worldevent":
                return ValidateWorldEvent(args);
            case "spawnboss":
                return ValidateSpawnBoss(args);
            case "spawnmob":
                return ValidateSpawnMob(args);
            case "give":
                return ValidateGive(args);
            case "heal":
            case "godmode":
            case "maxhp":
                return ValidateOptionalPlayer(verb, args);
            case "kill":
            case "kick":
            case "mute":
            case "slap":
                return ValidateRequiredPlayer(verb, args, allowExtraArgs: true);
            case "tp":
            case "tphere":
                return ValidateRequiredPlayer(verb, args, allowExtraArgs: false);
            case "warp":
                return ValidateWarp(args);
            case "maxspawns":
            case "spawnrate":
                return ValidateNumber(verb, args);
            default:
                if (NoArgCommands.Contains(verb))
                    return args.Length == 0
                        ? Valid(verb, null)
                        : Invalid($"{verb} {string.Join(" ", args)}", $"'{verb}' no admite argumentos");
                return Invalid(action, $"no conozco el comando '{verb}'");
        }
    }

    private static ActionValidation ValidateTime(string[] args)
    {
        if (args.Length != 1)
            return Invalid(null, "'time' requiere una hora o un momento del día");
        var arg = args[0].ToLowerInvariant();
        if (TimeNames.Contains(arg))
            return Valid($"time {arg}", null);
        if (int.TryParse(args[0], out var hour) && hour >= 0 && hour <= 23)
            return Valid($"time {hour}", null);
        return Invalid(null, $"'{args[0]}' no es una hora válida (0-23 o day/night/noon/dusk/midnight)");
    }

    private static ActionValidation ValidateBridge(string[] args)
    {
        if (args.Length == 0)
            return Invalid(null, "'bridge' requiere un subcomando");

        if (args[0].Equals("rain", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 2)
                return Invalid(null, "'bridge rain' requiere on, off o heavy");
            var mode = args[1].ToLowerInvariant();
            return RainModes.Contains(mode)
                ? Valid($"bridge rain {mode}", null)
                : Invalid(null, $"'{args[1]}' no es un modo de lluvia válido");
        }

        if (args[0].Equals("slime", StringComparison.OrdinalIgnoreCase) &&
            args.Length >= 2 && args[1].Equals("rain", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 3)
                return Invalid(null, "'bridge slime rain' requiere on u off");
            var mode = args[2].ToLowerInvariant();
            return SlimeRainModes.Contains(mode)
                ? Valid($"bridge slime rain {mode}", null)
                : Invalid(null, $"'{args[2]}' no es un modo válido");
        }

        return Invalid(null, $"'bridge {string.Join(" ", args)}' no es un comando conocido");
    }

    private static ActionValidation ValidateWorldEvent(string[] args)
    {
        if (args.Length != 1)
            return Invalid(null, "'worldevent' requiere un evento");
        var evt = args[0].ToLowerInvariant();
        return WorldEvents.Contains(evt)
            ? Valid($"worldevent {evt}", null)
            : Invalid(null, $"'{args[0]}' no es un evento conocido");
    }

    private static ActionValidation ValidateSpawnBoss(string[] args)
    {
        if (args.Length == 0)
            return Invalid(null, "'spawnboss' requiere un jefe");
        var name = string.Join(" ", args).Trim();
        var compact = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"\s+", "");
        if (BossAliases.TryGetValue(compact, out var canonical))
            return Valid($"spawnboss {canonical}", null);
        return Invalid(null, $"no conozco al jefe '{name}'");
    }

    private static ActionValidation ValidateSpawnMob(string[] args)
    {
        if (args.Length == 0)
            return Invalid(null, "'spawnmob' requiere un mob");
        var last = args[^1];
        var qty = 1;
        var mobTokens = args;
        if (int.TryParse(last, out var q) && q > 0 && q <= 999)
        {
            qty = q;
            mobTokens = args[..^1];
        }
        if (mobTokens.Length == 0)
            return Invalid(null, "'spawnmob' requiere un mob");
        var mob = string.Join(" ", mobTokens);
        return Valid($"spawnmob {mob} {qty}".TrimEnd(), null);
    }

    private static ActionValidation ValidateGive(string[] args)
    {
        if (args.Length < 2)
            return Invalid(null, "el comando 'give' necesita el item y el jugador");

        var rest = string.Join(" ", args);
        string item;
        string player;
        int qty = 1;

        if (rest.StartsWith("\""))
        {
            var end = rest.IndexOf('"', 1);
            if (end < 0)
                return Invalid(null, "las comillas del item no están cerradas");
            item = rest[1..end];
            var tail = rest[(end + 1)..].Trim();
            var tailParts = tail.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tailParts.Length == 0)
                return Invalid(null, "el comando 'give' necesita el jugador");
            if (tailParts.Length >= 2 && int.TryParse(tailParts[^1], out var q) && q > 0 && q <= 999)
            {
                qty = q;
                player = string.Join(" ", tailParts[..^1]);
            }
            else
            {
                player = tail;
            }
        }
        else
        {
            var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var consumed = parts.Length;
            if (parts.Length >= 2 && int.TryParse(parts[^1], out var q) && q > 0 && q <= 999)
            {
                qty = q;
                consumed = parts.Length - 1;
            }
            if (consumed < 2)
                return Invalid(null, "el comando 'give' necesita el item y el jugador");
            player = parts[consumed - 1];
            item = string.Join(" ", parts.Take(consumed - 1));
        }

        if (string.IsNullOrWhiteSpace(item) || string.IsNullOrWhiteSpace(player))
            return Invalid(null, "el comando 'give' necesita el item y el jugador");

        return new ActionValidation
        {
            IsValid = true,
            Action = $"give \"{item}\" {player} {qty}".TrimEnd(),
            TargetPlayer = player,
            Item = item,
            GiveTarget = player,
            Quantity = qty
        };
    }

    private static ActionValidation ValidateOptionalPlayer(string verb, string[] args)
    {
        if (args.Length == 0)
            return Valid(verb, null);
        if (args.Length == 1)
            return Valid($"{verb} {args[0]}", args[0]);
        return Invalid($"{verb} {string.Join(" ", args)}", $"'{verb}' admite a lo sumo un jugador");
    }

    private static ActionValidation ValidateRequiredPlayer(string verb, string[] args, bool allowExtraArgs)
    {
        if (args.Length == 0)
            return Invalid(verb, $"'{verb}' necesita un jugador");
        if (!allowExtraArgs && args.Length != 1)
            return Invalid(verb, $"'{verb}' admite exactamente un jugador");
        return Valid($"{verb} {string.Join(" ", args)}", args[0]);
    }

    private static ActionValidation ValidateWarp(string[] args)
    {
        if (args.Length == 0)
            return Invalid(null, "'warp' requiere list, add <nombre> o <nombre>");
        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase) && args.Length == 1)
            return Valid("warp list", null);
        if (args[0].Equals("add", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
            return Valid($"warp add {string.Join(" ", args.Skip(1))}", null);
        return Valid($"warp {string.Join(" ", args)}", null);
    }

    private static ActionValidation ValidateNumber(string verb, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var n) || n < 1 || n > 999)
            return Invalid(null, $"'{verb}' requiere un número entre 1 y 999");
        return Valid($"{verb} {n}", null);
    }

    private static ActionValidation Valid(string? action, string? targetPlayer)
        => new() { IsValid = true, Action = action, TargetPlayer = targetPlayer };

    private static ActionValidation Invalid(string? action, string reason)
        => new() { IsValid = false, Action = action, Reason = reason };
}
```

- [ ] **Step 5: Ejecutar los tests para verificar que pasan**

Run: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Terraria.Agent.Api.Tests/Terraria.Agent.Api.Tests.csproj -c Release`
Expected: PASS — todos los tests verdes (y compila también el proyecto API).

- [ ] **Step 6: Commit**

```bash
cd /home/roman/k8s-projects && git add terraria-agent/src/Terraria.Agent.Api.Tests terraria-agent/src/Terraria.Agent.Api/Services/ActionValidator.cs && git commit -m "test(agent): add ActionValidator unit tests + implementation"
```

---

### Task 2: `GroqRateLimiter` (TDD)

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Services/GroqRateLimiter.cs`
- Create: `terraria-agent/src/Terraria.Agent.Api.Tests/GroqRateLimiterTests.cs`
- Test: `terraria-agent/src/Terraria.Agent.Api.Tests/GroqRateLimiterTests.cs`

**Interfaces:**
- Consumes: config `Groq:MaxRequestsPerMinute` (default 28).
- Produces: `GroqRateLimiter` (class) con ctor `(IConfiguration config, ILogger<GroqRateLimiter> logger)` y método `Task WaitForSlotAsync()`. Lo inyectan `IntentParser` y `GroqService`.

- [ ] **Step 1: Escribir el test fallido**

Create `terraria-agent/src/Terraria.Agent.Api.Tests/GroqRateLimiterTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Terraria.Agent.Api.Services;
using Xunit;

namespace Terraria.Agent.Api.Tests;

public class GroqRateLimiterTests
{
    private static GroqRateLimiter BuildLimiter(string max)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Groq:MaxRequestsPerMinute"] = max
            })
            .Build();
        return new GroqRateLimiter(config, NullLogger<GroqRateLimiter>.Instance);
    }

    [Fact]
    public async Task RequestsWithinLimitProceedWithoutLongWaits()
    {
        var limiter = BuildLimiter("3");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < 3; i++)
            await limiter.WaitForSlotAsync();

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Expected quick acquisitions, took {stopwatch.Elapsed}");
    }
}
```

- [ ] **Step 2: Ejecutar el test para verificar que falla**

Run: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Terraria.Agent.Api.Tests/Terraria.Agent.Api.Tests.csproj -c Release`
Expected: FAIL — `CS0246` ("GroqRateLimiter could not be found").

- [ ] **Step 3: Escribir la implementación**

Create `terraria-agent/src/Terraria.Agent.Api/Services/GroqRateLimiter.cs`:

```csharp
namespace Terraria.Agent.Api.Services;

public class GroqRateLimiter
{
    private readonly int _maxPerMinute;
    private readonly Queue<DateTime> _callTimes = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<GroqRateLimiter> _logger;

    public GroqRateLimiter(IConfiguration config, ILogger<GroqRateLimiter> logger)
    {
        _logger = logger;
        _maxPerMinute = Math.Max(1, config.GetValue("Groq:MaxRequestsPerMinute", 28));
    }

    public async Task WaitForSlotAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            while (_callTimes.Count > 0 && (now - _callTimes.Peek()).TotalSeconds >= 60)
                _callTimes.Dequeue();

            if (_callTimes.Count >= _maxPerMinute)
            {
                var waitMs = (int)Math.Ceiling(60000 - (now - _callTimes.Peek()).TotalMilliseconds);
                if (waitMs > 0)
                {
                    _logger.LogInformation("Groq rate limiter: waiting {Ms}ms for a slot", waitMs);
                    await Task.Delay(waitMs);
                }
                _callTimes.Dequeue();
                _callTimes.Enqueue(DateTime.UtcNow);
                return;
            }

            _callTimes.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            _gate.Release();
        }
    }
}
```

- [ ] **Step 4: Ejecutar el test para verificar que pasa**

Run: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Terraria.Agent.Api.Tests/Terraria.Agent.Api.Tests.csproj -c Release`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /home/roman/k8s-projects && git add terraria-agent/src/Terraria.Agent.Api/Services/GroqRateLimiter.cs terraria-agent/src/Terraria.Agent.Api.Tests/GroqRateLimiterTests.cs && git commit -m "feat(agent): add shared Groq rate limiter"
```

---

### Task 3: `OnlinePlayersService`

**Files:**
- Create: `terraria-agent/src/Terraria.Agent.Api/Services/OnlinePlayersService.cs`

**Interfaces:**
- Consumes: `TShockClient.GetStatusAsync()` (devuelve `TerrariaStatus?` con `List<PlayerInfo> Players`), config `Agent:PlayersCacheSeconds` (default 5).
- Produces: `OnlinePlayersService` (class) con ctor `(TShockClient tshock, IConfiguration config, ILogger<OnlinePlayersService> logger)` y método `Task<OnlinePlayersSnapshot> GetSnapshotAsync()`. `OnlinePlayersSnapshot` (class): `bool IsReliable`, `List<string> Players`.

Nota: es un wrapper fino de HTTP+cache; no se le escriben unit tests (requeriría mocks). Se cubre con el E2E de la Task 8.

- [ ] **Step 1: Escribir la implementación**

Create `terraria-agent/src/Terraria.Agent.Api/Services/OnlinePlayersService.cs`:

```csharp
namespace Terraria.Agent.Api.Services;

public sealed class OnlinePlayersSnapshot
{
    public bool IsReliable { get; init; }
    public List<string> Players { get; init; } = new();
}

public class OnlinePlayersService
{
    private readonly TShockClient _tshock;
    private readonly ILogger<OnlinePlayersService> _logger;
    private readonly TimeSpan _cacheDuration;

    private List<string>? _cache;
    private DateTime _cacheTime;
    private bool _cacheReliable;

    public OnlinePlayersService(TShockClient tshock, IConfiguration config, ILogger<OnlinePlayersService> logger)
    {
        _tshock = tshock;
        _logger = logger;
        _cacheDuration = TimeSpan.FromSeconds(Math.Max(1, config.GetValue("Agent:PlayersCacheSeconds", 5)));
    }

    public async Task<OnlinePlayersSnapshot> GetSnapshotAsync()
    {
        if (_cache != null && DateTime.UtcNow - _cacheTime < _cacheDuration)
            return new OnlinePlayersSnapshot { IsReliable = _cacheReliable, Players = _cache };

        try
        {
            var status = await _tshock.GetStatusAsync();
            var players = status?.Players
                .Select(p => p.Name.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            _cache = players;
            _cacheReliable = status != null;
            _cacheTime = DateTime.UtcNow;

            _logger.LogInformation("Online players refreshed: {Count} players ({Reliable})", players.Count, _cacheReliable);
            return new OnlinePlayersSnapshot { IsReliable = _cacheReliable, Players = players };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get online players; disabling guard until next refresh");
            _cache = new List<string>();
            _cacheReliable = false;
            _cacheTime = DateTime.UtcNow;
            return new OnlinePlayersSnapshot { IsReliable = false, Players = _cache };
        }
    }
}
```

- [ ] **Step 2: Compilar**

Run: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release`
Expected: build OK (0 errores).

- [ ] **Step 3: Commit**

```bash
cd /home/roman/k8s-projects && git add terraria-agent/src/Terraria.Agent.Api/Services/OnlinePlayersService.cs && git commit -m "feat(agent): add online players service with short cache"
```

---

### Task 4: DI + configuración

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Program.cs`
- Modify: `terraria-agent/src/Terraria.Agent.Api/appsettings.json`

**Interfaces:**
- Consumes: `GroqRateLimiter`, `ActionValidator`, `OnlinePlayersService` (Tasks 1-3).
- Produces: los tres servicios registrados en DI y disponibles para `ChatController`, `IntentParser` y `GroqService` en Tasks 5-7.

- [ ] **Step 1: Registrar los servicios en Program.cs**

En `Program.cs`, tras la línea `builder.Services.AddHostedService<AutoEventService>();` (línea 16), añadir:

```csharp
builder.Services.AddSingleton<GroqRateLimiter>();
builder.Services.AddSingleton<ActionValidator>();
builder.Services.AddSingleton<OnlinePlayersService>();
```

El bloque de registros queda:

```csharp
builder.Services.AddSingleton<ChatHistory>();
builder.Services.AddSingleton<CommandParser>();
builder.Services.AddHttpClient<WikiService>();
builder.Services.AddSingleton<CraftingService>();
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddHttpClient<TShockClient>();
builder.Services.AddHttpClient<GroqService>();
builder.Services.AddHttpClient<IntentParser>();
builder.Services.AddHostedService<AutoEventService>();
builder.Services.AddSingleton<GroqRateLimiter>();
builder.Services.AddSingleton<ActionValidator>();
builder.Services.AddSingleton<OnlinePlayersService>();
```

- [ ] **Step 2: Añadir configuración en appsettings.json**

En `terraria-agent/src/Terraria.Agent.Api/appsettings.json`, en la sección `"Agent"` añadir `"PlayersCacheSeconds": 5` y en la sección `"Groq"` añadir `"MaxRequestsPerMinute": 28`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Agent": {
    "Token": "terraria-agent-secret-token-2024",
    "PlayersCacheSeconds": 5
  },
  "TShock": {
    "Url": "http://terraria-server:7878",
    "Token": "terraria-agent-secret-token-2024",
    "PluginUrl": "http://terraria-server:7879"
  },
  "Groq": {
    "ApiKey": "",
    "Model": "llama-3.3-70b-versatile",
    "Endpoint": "https://api.groq.com/openai/v1/chat/completions",
    "MaxRequestsPerMinute": 28
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

- [ ] **Step 3: Compilar**

Run: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release`
Expected: build OK.

- [ ] **Step 4: Commit**

```bash
cd /home/roman/k8s-projects && git add terraria-agent/src/Terraria.Agent.Api/Program.cs terraria-agent/src/Terraria.Agent.Api/appsettings.json && git commit -m "feat(agent): register hardening services + config defaults"
```

---

### Task 5: `ChatController` — validación y guardia en Ruta 3

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs`

**Interfaces:**
- Consumes: `ActionValidator.Validate(string?)` → `ActionValidation`; `OnlinePlayersService.GetSnapshotAsync()` → `OnlinePlayersSnapshot`. Métodos existentes `HandleGiveAsync(ChatEvent, GiveRequest)`, `HandleMaxHpAsync(ChatEvent, string)`, `LooksLikeCommandFailure(string?)`, `IsMaxHpAction(string)`, record `GiveRequest(string Item, string Target, int Quantity)`.
- Produces: el flujo de Ruta 3 valida cada acción, bloquea inválidos/offline con narración honesta, enruta `give` a `HandleGiveAsync`, y reemplaza narración genérica en comandos mecánicos.

- [ ] **Step 1: Añadir campos y parámetros de constructor**

Reemplazar el bloque de campos (líneas 18-25):

```csharp
    private readonly CommandParser _parser;
    private readonly TShockClient _tshock;
    private readonly GroqService _groq;
    private readonly IntentParser _intentParser;
    private readonly ChatHistory _history;
    private readonly ILogger<ChatController> _logger;
    private readonly IConfiguration _config;
    private readonly bool _readOnly;
```

por:

```csharp
    private readonly CommandParser _parser;
    private readonly TShockClient _tshock;
    private readonly GroqService _groq;
    private readonly IntentParser _intentParser;
    private readonly ChatHistory _history;
    private readonly ActionValidator _actionValidator;
    private readonly OnlinePlayersService _onlinePlayers;
    private readonly ILogger<ChatController> _logger;
    private readonly IConfiguration _config;
    private readonly bool _readOnly;
```

Reemplazar la firma del constructor (líneas 149-156):

```csharp
    public ChatController(
        CommandParser parser,
        TShockClient tshock,
        GroqService groq,
        IntentParser intentParser,
        ChatHistory history,
        ILogger<ChatController> logger,
        IConfiguration config)
```

por:

```csharp
    public ChatController(
        CommandParser parser,
        TShockClient tshock,
        GroqService groq,
        IntentParser intentParser,
        ChatHistory history,
        ActionValidator actionValidator,
        OnlinePlayersService onlinePlayers,
        ILogger<ChatController> logger,
        IConfiguration config)
```

y añadir las asignaciones tras `_history = history;`:

```csharp
        _actionValidator = actionValidator;
        _onlinePlayers = onlinePlayers;
```

- [ ] **Step 2: Reemplazar la sección de ejecución de acción en Ruta 3**

Reemplazar el bloque que va desde `// Execute TShock action if detected` (línea 319) hasta `return Ok(new { narration = intent.Narration, action = intent.Action });` (línea 352):

```csharp
        // Execute TShock action if detected
        if (!string.IsNullOrWhiteSpace(intent.Action))
        {
            if (_readOnly)
            {
                _logger.LogInformation("Read-only mode: skipping action {Action}", intent.Action);
            }
            else if (IsMaxHpAction(intent.Action))
            {
                var narration = await HandleMaxHpAsync(chatEvent, intent.Action);
                await BroadcastMessageAsync($"[Narrador] {narration}");
                await _history.SaveMessageAsync(chatEvent.Player, "assistant", narration);
                return Ok(new { narration = narration, action = intent.Action });
            }
            else
            {
                _logger.LogInformation("Executing action: {Action}", intent.Action);
                var response = await _tshock.ExecuteCommandAsync(intent.Action);
                if (LooksLikeCommandFailure(response))
                {
                    _logger.LogWarning("Action {Action} reported failure: {Response}", intent.Action, response);
                    var honest = $"Lo intenté, pero el servidor rechazó el comando. {intent.Narration}";
                    await BroadcastMessageAsync($"[Narrador] {honest}");
                    await _history.SaveMessageAsync(chatEvent.Player, "assistant", honest);
                    return Ok(new { narration = honest, action = intent.Action, failure = true });
                }
            }
        }

        // Broadcast narration
        await BroadcastMessageAsync($"[Narrador] {intent.Narration}");

        // Return narration in response body for testing/API consumers
        return Ok(new { narration = intent.Narration, action = intent.Action });
```

por:

```csharp
        // Execute TShock action if detected
        var executedAction = intent.Action;
        if (!string.IsNullOrWhiteSpace(intent.Action))
        {
            if (_readOnly)
            {
                _logger.LogInformation("Read-only mode: skipping action {Action}", intent.Action);
            }
            else
            {
                var validation = _actionValidator.Validate(intent.Action);

                if (!validation.IsValid)
                {
                    var honest = $"No puedo ejecutar eso. {validation.Reason}";
                    _logger.LogInformation("Rejected action {Action} from {Player}: {Reason}",
                        intent.Action, chatEvent.Player, validation.Reason);
                    await BroadcastMessageAsync($"[Narrador] {honest}");
                    await _history.SaveMessageAsync(chatEvent.Player, "assistant", honest);
                    return Ok(new { narration = honest, action = intent.Action, failure = true });
                }

                executedAction = validation.Action;

                if (!string.IsNullOrEmpty(validation.TargetPlayer))
                {
                    var snapshot = await _onlinePlayers.GetSnapshotAsync();
                    if (snapshot.IsReliable &&
                        !snapshot.Players.Contains(validation.TargetPlayer, StringComparer.OrdinalIgnoreCase))
                    {
                        var honest = $"{validation.TargetPlayer} no está conectado ahora mismo.";
                        _logger.LogInformation("Rejected action {Action} from {Player}: target {Target} offline",
                            intent.Action, chatEvent.Player, validation.TargetPlayer);
                        await BroadcastMessageAsync($"[Narrador] {honest}");
                        await _history.SaveMessageAsync(chatEvent.Player, "assistant", honest);
                        return Ok(new { narration = honest, action = executedAction, failure = true });
                    }
                }

                if (validation.Item != null)
                {
                    var req = new GiveRequest(validation.Item, validation.GiveTarget!, validation.Quantity);
                    return await HandleGiveAsync(chatEvent, req);
                }

                if (IsMaxHpAction(executedAction))
                {
                    var narration = await HandleMaxHpAsync(chatEvent, executedAction);
                    await BroadcastMessageAsync($"[Narrador] {narration}");
                    await _history.SaveMessageAsync(chatEvent.Player, "assistant", narration);
                    return Ok(new { narration = narration, action = executedAction });
                }

                _logger.LogInformation("Executing action: {Action}", executedAction);
                var response = await _tshock.ExecuteCommandAsync(executedAction);
                if (LooksLikeCommandFailure(response))
                {
                    _logger.LogWarning("Action {Action} reported failure: {Response}", executedAction, response);
                    var honest = $"Lo intenté, pero el servidor rechazó el comando. {intent.Narration}";
                    await BroadcastMessageAsync($"[Narrador] {honest}");
                    await _history.SaveMessageAsync(chatEvent.Player, "assistant", honest);
                    return Ok(new { narration = honest, action = executedAction, failure = true });
                }

                if (IsMechanical(executedAction) && IsGenericNarration(intent.Narration))
                    intent.Narration = $"Hecho: {executedAction}";
            }
        }

        // Broadcast narration
        await BroadcastMessageAsync($"[Narrador] {intent.Narration}");

        // Return narration in response body for testing/API consumers
        return Ok(new { narration = intent.Narration, action = executedAction });
```

- [ ] **Step 3: Añadir los helpers de narración mecánica**

Al final de la clase `ChatController` (tras el método `HandlePeligro`, antes de la llave de cierre), añadir:

```csharp
    private static readonly HashSet<string> MechanicalCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "time", "worldevent", "hardmode", "save", "setspawn", "settle", "butcher", "maxspawns", "spawnrate"
    };

    private static bool IsMechanical(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return false;
        return MechanicalCommands.Contains(action.Trim().Split(' ')[0].TrimStart('/'));
    }

    private static bool IsGenericNarration(string? narration)
    {
        if (string.IsNullOrWhiteSpace(narration)) return true;
        var text = narration.Trim();
        if (text.Length < 40) return true;
        foreach (var prefix in new[] { "he hecho", "comando ejecutado", "el comando", "hecho:", "se ejecuta" })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
```

- [ ] **Step 4: Compilar**

Run: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release`
Expected: build OK (los tests de Task 1 siguen pasando; ejecutar también `dotnet test`).

- [ ] **Step 5: Commit**

```bash
cd /home/roman/k8s-projects && git add terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs && git commit -m "feat(agent): validate and guard Groq actions in chat route"
```

---

### Task 6: `IntentParser` — prompt estricto, backoff y jugadores online

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Services/IntentParser.cs`

**Interfaces:**
- Consumes: `OnlinePlayersService.GetSnapshotAsync()`, `GroqRateLimiter.WaitForSlotAsync()`, `ChatHistory` (existente).
- Produces: `IntentResult` con narración siempre no-vacía (o `null` solo en errores no-429); prompt con few-shot, anti-alucinación y `{online_players}`.

- [ ] **Step 1: Añadir campos y parámetros de constructor**

Añadir campos junto a los existentes (líneas 9-16):

```csharp
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly ILogger<IntentParser> _logger;
    private readonly CraftingService _crafting;
    private readonly KnowledgeService _knowledge;
    private readonly ChatHistory _history;
    private readonly OnlinePlayersService _onlinePlayers;
    private readonly GroqRateLimiter _rateLimiter;
```

Reemplazar la firma del constructor (líneas 103-114):

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

por:

```csharp
    public IntentParser(HttpClient httpClient, IConfiguration config, ILogger<IntentParser> logger,
        CraftingService crafting, KnowledgeService knowledge, ChatHistory history,
        OnlinePlayersService onlinePlayers, GroqRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
        _crafting = crafting;
        _knowledge = knowledge;
        _history = history;
        _onlinePlayers = onlinePlayers;
        _rateLimiter = rateLimiter;
    }
```

- [ ] **Step 2: Reemplazar el `SystemPrompt` completo**

Reemplazar la constante `SystemPrompt` (desde `private const string SystemPrompt = @"Eres NARRADOR...` hasta el `";` que la cierra en línea 101) por:

```csharp
    private const string SystemPrompt = @"Eres NARRADOR, el narrador épico del mundo 'MundoSobrinos' en Terraria (Master difficulty).
Tienes personalidad: dramático, gracioso, un poco exagerado, pero siempre útil. Juegas con sobrinos. Español casual.

FORMATO DE RESPUESTA (OBLIGATORIO):
- Responde SOLO con JSON. Sin markdown, sin bloques de codigo, sin texto alrededor.
- Claves EXACTAS en inglés: {""respond"": true/false, ""action"": ""<comando o null>"", ""narration"": ""<texto>""}
- 'narration' SIEMPRE con texto (al menos 3 palabras).

ANTI-ALUCINACIÓN (MUY IMPORTANTE):
- action SOLO puede ser UNO de los comandos de la lista, con su sintaxis EXACTA, o null.
- Si el jugador pide algo cuyo comando NO está en la lista, action DEBE ser null y pregunta en la narration.
- NUNCA inventes comandos. NUNCA modifiques la sintaxis. NUNCA inventes jugadores ni mobs ni items.
- PROHIBIDO: ""buff"" — NO funciona en el servidor.

HONESTIDAD:
- La narration describe lo que el comando EJECUTA, no el resultado. NUNCA afirmes que se entregó o apareció algo que el servidor puede rechazar.
- Para give/spawnboss/spawnmob: describe la acción (""entrego X a Y"", ""invoco a Z""), NO afirmes el resultado.
- Si el comando puede fallar (jugador offline, item no existe), dilo con honestidad.

CONTEXTO DEL MUNDO:
{world_status}

DATOS RELEVANTES:
{knowledge_context}

JUGADORES ONLINE (usa SOLO estos nombres como destinatarios):
{online_players}

COMANDOS DISPONIBLES (valores EXACTOS para 'action', o null si no hay comando):
TIEMPO:
  - ""time day"", ""time night"", ""time noon"", ""time dusk"", ""time midnight""
  - Hora EXACTA: ""time <hora 0-23>"". Ej: 10 AM = ""time 10"", 10 PM = ""time 22"", mediodía = ""time 12"" (NO confundir 10 AM con noon)
CLIMA Y EVENTOS DEL MUNDO:
  - Lluvia: ""bridge rain on"", ""bridge rain off"", ""bridge rain heavy""
  - ""worldevent bloodmoon"", ""worldevent eclipse"", ""worldevent fullmoon"", ""worldevent sandstorm"", ""worldevent meteor""
  - ""worldevent lanternsnight"" (noche de linternas), ""worldevent meteorshower"" (lluvia de estrellas), ""worldevent coinrain"" (lluvia de monedas)
  - ""worldevent star"" (noche de estrellas fugaces), ""worldevent halloween"", ""worldevent xmas""
  - Lluvia de slimes: ""bridge slime rain on"", ""bridge slime rain off""
  - Hardmode: ""hardmode""
INVASIONES:
  - ""worldevent goblins"", ""worldevent pirates"", ""worldevent martians""
BOSSES (NUNCA confundir):
  - Eye of Cthulhu = ""spawnboss EyeOfCthulhu"". Español: ojo, eyeborg, cthulhu
  - The Twins (Retinazer+Spazmatism) = ""spawnboss TheTwins"". Español: gemelos, mellizos, los dos ojos, retinazer, spazmatism
  - Wall of Flesh = ""spawnboss WallOfFlesh"". Español: muralla de carne, pared de carne, muro de carne, wall of flesh, wof. MUY IMPORTANTE: 'muralla de carne'/'pared de carne' ES Wall of Flesh, NO el Eater of Worlds (devoramundos/gusano).
  - Otros: ""spawnboss KingSlime"", ""spawnboss EaterOfWorlds"", ""spawnboss Skeletron"", ""spawnboss QueenBee"", ""spawnboss TheDestroyer"", ""spawnboss SkeletronPrime"", ""spawnboss Plantera"", ""spawnboss Golem"", ""spawnboss LunaticCultist"", ""spawnboss MoonLord""
MOBS:
  - ""spawnmob <mob> [cantidad]"" (ej: ""spawnmob zombie 10"")
JUGADORES:
  - Curar: ""heal [jugador]"" (sin jugador = quien pide)
  - SUBIR VIDA MÁXIMA: ""maxhp [jugador]"" (da Cristal de Vida + Fruta de Vida; el máximo sube hasta 500)
  - Dar item: ""give ""<item>"" <jugador> [cantidad]"" — el ITEM va PRIMERO entre COMILLAS y el jugador DESPUÉS. Cantidad por defecto 1. El DESTINATARIO es SIEMPRE el jugador que pide (""dame X"") salvo que pida dárselo a otro. Ej: ""give ""Iron Bar"" Testeador1 20"". NUNCA escribas ""give"" incompleto sin item ni jugador.
  - ""godmode [jugador]"" - ""kill <jugador>"" - ""kick <jugador> [razón]"" - ""mute <jugador>"" - ""slap <jugador>""
TELEPORTACIÓN:
  - ""tp <jugador>"", ""tphere <jugador>"", ""home"", ""spawn"", ""warp <nombre>"", ""warp list"", ""warp add <nombre>""
MUNDO:
  - ""setspawn"", ""settle"", ""butcher"", ""maxspawns <n>"", ""spawnrate <n>"", ""save""

REGLAS DE FRASES:
- ""Para"" al inicio de frase = ""Parar"" (stop). Ej: ""para la lluvia"" = bridge rain off, ""para la lluvia de slimes"" = bridge slime rain off
- ""Quiero lluvia"" = bridge rain on. ""No quiero lluvia"" = bridge rain off.
- Si piden una hora concreta (ej ""que sean las 10"", ""pon las 15"") usa ""time <hora>"" con el número EXACTO. 10 AM = ""time 10"", NO ""time noon"" (noon es 12 PM).
- ""subeme la vida maxima"", ""dame mas vida"", ""me quiero curar al maximo"" = ""maxhp <jugador>"" con el nombre del jugador que pide.
- ""dame <item>"", ""regalame <item>"", ""creame <item>"" = ""give ""<item>"" <jugador> [cantidad]"" con el jugador que pide como destinatario. NUNCA entregues a otro jugador distinto del que pide.

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

EJEMPLOS (sigue el formato EXACTO):
Jugador: ""pon las 10 de la mañana""
{""respond"": true, ""action"": ""time 10"", ""narration"": ""Ajusto el reloj del mundo a las diez. ¡Día radiante, héroes!""}

Jugador: ""invoca al ojo""
{""respond"": true, ""action"": ""spawnboss EyeOfCthulhu"", ""narration"": ""¡El Ojo de Cthulhu se agita en la oscuridad! Prepárense."""}

Jugador: ""dame un lingote de hierro""
{""respond"": true, ""action"": ""give ""Iron Bar"" Testeador1 1"", ""narration"": ""Te entrego un Lingote de Hierro. ¡A forjar!""}

Jugador: ""invoca a medusa""
{""respond"": true, ""action"": null, ""narration"": ""No conozco a la Medusa como jefe invocable. ¿Te refieres al Ojo de Cthulhu o al Rey Slime?""}

Jugador: ""ja ja""
{""respond"": false, ""action"": null, ""narration"": ""}";
```

Nota: al estar en un verbatim string C# (`@"..."`), las comillas interiores se escapan duplicándolas (`""`). No usar interpolación `$@"..."` (las llaves `{...}` del JSON se tratarían como tokens de interpolación).

- [ ] **Step 3: Inyectar jugadores online en el prompt**

Reemplazar el bloque de construcción del system message (líneas 139-144):

```csharp
            // Build world status
            var worldStatus = _knowledge.GetGameContext();

            var systemMessage = SystemPrompt
                .Replace("{world_status}", worldStatus)
                .Replace("{knowledge_context}", knowledgeContext);
```

por:

```csharp
            // Build world status + online players
            var worldStatus = _knowledge.GetGameContext();
            var playersSnapshot = await _onlinePlayers.GetSnapshotAsync();
            var onlinePlayers = playersSnapshot.Players.Count > 0
                ? string.Join(", ", playersSnapshot.Players)
                : "ninguno";

            var systemMessage = SystemPrompt
                .Replace("{world_status}", worldStatus)
                .Replace("{knowledge_context}", knowledgeContext)
                .Replace("{online_players}", onlinePlayers);
```

- [ ] **Step 4: Backoff exponencial con rate limiter compartido**

Reemplazar el bloque de envío con retry único (líneas 168-188):

```csharp
            var response = await _httpClient.SendAsync(httpRequest);

            // Retry once on rate limit after 12s delay
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Groq rate limited, retrying in 12s...");
                await Task.Delay(12000);
                httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = JsonContent.Create(request)
                };
                httpRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                response = await _httpClient.SendAsync(httpRequest);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq API returned {StatusCode}", response.StatusCode);
                return null;
            }
```

por:

```csharp
            // Send with shared rate limiter + exponential backoff on 429
            HttpResponseMessage response;
            var attempt = 0;
            while (true)
            {
                attempt++;
                await _rateLimiter.WaitForSlotAsync();

                httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = JsonContent.Create(request)
                };
                httpRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

                response = await _httpClient.SendAsync(httpRequest);
                if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests || attempt >= 3)
                    break;

                var delayMs = attempt == 1 ? 12000 : 24000;
                _logger.LogWarning("Groq rate limited (attempt {Attempt}/3), retrying in {Delay}ms", attempt, delayMs);
                await Task.Delay(delayMs);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Groq still rate limited after {Attempt} attempts", attempt);
                var saturated = new IntentResult
                {
                    Respond = true,
                    Action = null,
                    Narration = "¡Estoy saturado de peticiones! Repítemelo en un momento, héroe."
                };
                await _history.SaveMessageAsync(player, "user", chatEvent.Text);
                await _history.SaveMessageAsync(player, "assistant", saturated.Narration);
                return saturated;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq API returned {StatusCode}", response.StatusCode);
                return null;
            }
```

- [ ] **Step 5: Narración por defecto**

Reemplazar el warning final (líneas 298-299):

```csharp
            if (string.IsNullOrWhiteSpace(result.Narration))
                _logger.LogWarning("Intent parser could not extract narration from Groq response: {Content}", json[..Math.Min(300, json.Length)]);
```

por:

```csharp
            if (string.IsNullOrWhiteSpace(result.Narration))
            {
                _logger.LogWarning("Intent parser could not extract narration; using default fallback. Content: {Content}",
                    json[..Math.Min(300, json.Length)]);
                result.Narration = "¡Cuéntamelo otra vez, héroe!";
                result.Respond = true;
                result.Action = null;
            }
```

- [ ] **Step 6: Compilar**

Run: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release`
Expected: build OK.

- [ ] **Step 7: Commit**

```bash
cd /home/roman/k8s-projects && git add terraria-agent/src/Terraria.Agent.Api/Services/IntentParser.cs && git commit -m "feat(agent): stricter prompt, exponential backoff, default narration, online players injection"
```

---

### Task 7: `GroqService` — rate limiter compartido

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs`

**Interfaces:**
- Consumes: `GroqRateLimiter.WaitForSlotAsync()`.
- Produces: `GroqService` con ctor `(HttpClient, IConfiguration, ILogger<GroqService>, GroqRateLimiter)`. `AutoEventService` no cambia: al usar `GroqService`, queda sujeto al mismo rate limiter.

- [ ] **Step 1: Añadir campo y parámetro**

Añadir campo junto a los existentes (líneas 9-13):

```csharp
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly ILogger<GroqService> _logger;
    private readonly GroqRateLimiter _rateLimiter;
```

Reemplazar la firma del constructor (líneas 24-31):

```csharp
    public GroqService(HttpClient httpClient, IConfiguration config, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
    }
```

por:

```csharp
    public GroqService(HttpClient httpClient, IConfiguration config, ILogger<GroqService> logger, GroqRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }
```

- [ ] **Step 2: Esperar slot antes de cada envío**

En `GenerateNarrationAsync`, inmediatamente antes de `var response = await _httpClient.SendAsync(httpRequest);` (línea 60), añadir:

```csharp
            await _rateLimiter.WaitForSlotAsync();
```

En `GenerateEventNarrationAsync`, inmediatamente antes de `var response = await _httpClient.SendAsync(httpRequest);` (línea 124), añadir:

```csharp
            await _rateLimiter.WaitForSlotAsync();
```

- [ ] **Step 3: Compilar**

Run: `docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release`
Expected: build OK.

- [ ] **Step 4: Commit**

```bash
cd /home/roman/k8s-projects && git add terraria-agent/src/Terraria.Agent.Api/Services/GroqService.cs && git commit -m "feat(agent): apply shared rate limiter to GroqService"
```

---

### Task 8: Build, tests, despliegue local y smoke E2E

**Files:**
- Modify: `terraria-agent/k8s/deployment.yaml` (solo la línea de `image:`)

**Interfaces:**
- Consumes: todo el código de Tasks 1-7.

- [ ] **Step 1: Ejecutar tests + build completos**

Run:
```bash
docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Terraria.Agent.Api.Tests/Terraria.Agent.Api.Tests.csproj -c Release
docker run --rm -v /home/roman/k8s-projects/terraria-agent/src:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release
```
Expected: todos los tests PASS y build 0 errores.

- [ ] **Step 2: Guardar el mundo (REGLA 1) y verificar pods**

Run:
```bash
POD=$(sudo k3s kubectl get pods -n terraria -l app=terraria-server -o jsonpath='{.items[0].metadata.name}')
sudo k3s kubectl exec -n terraria "$POD" -- /bin/sh -c 'curl -s "http://localhost:7878/v3/server/rawcmd?cmd=/save&token=terraria-agent-secret-token-2024"'
sudo k3s kubectl get pods -n terraria
```
Expected: respuesta del save (200) y pods 1/1 Ready.

- [ ] **Step 3: Build imagen con tag único (REGLA 3)**

Run:
```bash
cd /home/roman/k8s-projects/terraria-agent && docker build --no-cache -t terraria-agent:v4-hardening ./terraria-agent/
```
Expected: build OK.

- [ ] **Step 4: Actualizar la imagen en el deployment local**

En `terraria-agent/k8s/deployment.yaml`, cambiar:
```yaml
          image: terraria-agent:v3-givefix
```
por:
```yaml
          image: terraria-agent:v4-hardening
```

- [ ] **Step 5: Importar y desplegar en local**

Run:
```bash
docker save terraria-agent:v4-hardening | sudo k3s ctr images import -
sudo k3s kubectl set image deployment/terraria-agent agent=terraria-agent:v4-hardening -n terraria
sudo k3s kubectl rollout status deployment/terraria-agent -n terraria --timeout=180s
sudo k3s kubectl get pods -n terraria
```
Expected: rollout completo y pod 1/1 Ready.

- [ ] **Step 6: Smoke E2E contra la ruta `/terraria-agent/api/chat`**

Run cada uno (deja ~5s entre llamadas para no saturar Groq) y comprueba manualmente que la respuesta es honesta (nunca silencio, nunca falso éxito):

```bash
BASE="http://172.30.138.92:30808/terraria-agent/api/chat"
curl -s -m 90 -X POST "$BASE" -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"Player":"tester","Text":"pon las 10 de la mañana"}'
# Esperado: 200, action "time 10", narration presente ("Hecho: time 10" u otra)

sleep 5
curl -s -m 90 -X POST "$BASE" -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"Player":"tester","Text":"invoca a medusa"}'
# Esperado: 200, NUNCA action "spawnboss Medusa" ejecutado; o bien narration preguntando ("No conozco a la Medusa...") o "No puedo ejecutar eso"

sleep 5
curl -s -m 90 -X POST "$BASE" -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"Player":"tester","Text":"guarda el mundo"}'
# Esperado: 200, action "save", narration presente

sleep 5
curl -s -m 90 -X POST "$BASE" -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"Player":"tester","Text":"dame la ceni"}'
# Esperado: 200, give "Ash Block" a tester (regresión Route 2c)
```

Además, comprobar en los logs del pod que no hay spam de reintentos ni excepciones:
```bash
AGENT_POD=$(sudo k3s kubectl get pods -n terraria -l app=terraria-agent -o jsonpath='{.items[0].metadata.name}')
sudo k3s kubectl logs -n terraria "$AGENT_POD" --tail=50
```

- [ ] **Step 7: Commit**

```bash
cd /home/roman/k8s-projects && git add terraria-agent/k8s/deployment.yaml && git commit -m "feat(agent): build and deploy agent hardening to local cluster"
```

---

### Task 9: Sync y despliegue en remoto (REGLA 2)

**Files:**
- Modify: `terraria-agent/k8s/deployment.yaml` (si la imagen del remoto difiere; normalmente el mismo manifest con tag `v4-hardening`)

**Interfaces:**
- Consumes: imagen `terraria-agent:v4-hardening` ya construida en local.

- [ ] **Step 1: Guardar el mundo en remoto (REGLA 1)**

Run:
```bash
ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl exec -n terraria \$(sudo k3s kubectl get pods -n terraria -l app=terraria-server -o jsonpath='{.items[0].metadata.name}') -- /bin/sh -c 'curl -s \"http://localhost:7878/v3/server/rawcmd?cmd=/save&token=terraria-agent-secret-token-2024\"'"
```
Expected: respuesta del save (200).

- [ ] **Step 2: Transferir imagen y desplegar en remoto**

Run:
```bash
docker save terraria-agent:v4-hardening | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"
ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl set image deployment/terraria-agent agent=terraria-agent:v4-hardening -n terraria"
ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout status deployment/terraria-agent -n terraria --timeout=180s"
ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl get pods -n terraria"
```
Expected: rollout completo y pod 1/1 Ready en remoto.

- [ ] **Step 3: Smoke E2E en remoto**

Run:
```bash
BASE="http://gaming.andalusiaone.com:30808/terraria-agent/api/chat"
curl -s -m 90 -X POST "$BASE" -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"Player":"tester","Text":"pon las 10 de la mañana"}'
curl -s -m 90 -X POST "$BASE" -H "Content-Type: application/json" \
  -H "X-Agent-Token: terraria-agent-secret-token-2024" \
  -d '{"Player":"tester","Text":"invoca a medusa"}'
```
Expected: 200 con narración honesta en ambos (igual criterio que Task 8).

- [ ] **Step 4: Commit (si el manifest remoto requirió cambio)**

```bash
cd /home/roman/k8s-projects && git add terraria-agent/k8s/deployment.yaml && git commit -m "feat(agent): sync agent hardening to remote cluster"
```
Si no hubo cambio de manifest, no hay commit (el manifest ya se commiteó en Task 8).
