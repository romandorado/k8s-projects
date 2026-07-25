# Agent Read-Only Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only mode flag that disables TShock command execution while keeping narration, crafting queries, and chat functionality working.

**Architecture:** Environment variable `AGENT_READ_ONLY` (true/false) controls the mode. When enabled, the agent narrates and answers questions but does NOT execute any game commands (time changes, boss spawns, weather, invasions, etc.).

**Tech Stack:** .NET 10, C#, Kubernetes Environment Variables

## Global Constraints
- Runtime: .NET 10
- Environment variable: `AGENT_READ_ONLY` (default: `false`)
- Behavior: When read-only, skip TShock command execution but still return narration
- No breaking changes to existing functionality

---

## Task 1: Add Read-Only Flag to ChatController

**Files:**
- Modify: `terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs`

**Interfaces:**
- Consumes: `IConfiguration` (already injected)
- Produces: Boolean `_readOnly` field checked before command execution

### Step 1: Add readonly field and constructor parameter

```csharp
// Add field
private readonly bool _readOnly;

// In constructor, add:
_readOnly = config.GetValue<bool>("Agent:ReadOnly", false);
```

### Step 2: Add using for Configuration

```csharp
// Add at top if not present
using Microsoft.Extensions.Configuration;
```

### Step 3: Check flag before executing action (line ~96-101)

```csharp
// Replace:
if (!string.IsNullOrWhiteSpace(intent.Action))
{
    _logger.LogInformation("Executing action: {Action}", intent.Action);
    await _tshock.ExecuteCommandAsync(intent.Action);
}

// With:
if (!string.IsNullOrWhiteSpace(intent.Action))
{
    if (_readOnly)
    {
        _logger.LogInformation("Read-only mode: skipping action {Action}", intent.Action);
    }
    else
    {
        _logger.LogInformation("Executing action: {Action}", intent.Action);
        await _tshock.ExecuteCommandAsync(intent.Action);
    }
}
```

### Step 4: Check flag in HandleAgentCommand (line ~129-161)

```csharp
// In HandleAgentCommand, check before executing commands that change game state
// Commands that should be blocked in read-only: Invocar, Tiempo, Clima
// Commands that should still work: Narrar, Hora, Consejo, Peligro, Help

private async Task<IActionResult> HandleAgentCommand(AgentCommand command)
{
    var commandType = _parser.GetCommandType(command);
    _logger.LogInformation("Agent command: {CommandType} from {Player}", commandType, command.Player);

    // Block game-changing commands in read-only mode
    if (_readOnly && commandType is CommandType.Invocar or CommandType.Tiempo or CommandType.Clima)
    {
        var readOnlyMessage = "🔒 El narrador está en modo solo lectura. No puedo ejecutar comandos que cambien el mundo.";
        await _tshock.BroadcastMessageAsync($"[Agent] {readOnlyMessage}");
        return Ok(new { narration = readOnlyMessage, command = commandType.ToString() });
    }

    string narration;
    try
    {
        narration = commandType switch
        {
            // ... rest unchanged
        };
    }
    // ... rest unchanged
}
```

### Step 5: Verify it compiles

Run: `cd terraria-agent && docker build -t terraria-agent:latest .`
Expected: Build succeeds

### Step 6: Commit

```bash
git add terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs
git commit -m "feat(agent): add read-only mode flag to disable game commands"
```

---

## Task 2: Add Environment Variable to Kubernetes

**Files:**
- Modify: `terraria-agent/k8s/deployment.yaml`

### Step 1: Add AGENT_READ_ONLY env var

```yaml
- name: AGENT_READ_ONLY
  value: "false"
```

### Step 2: Commit

```bash
git add terraria-agent/k8s/deployment.yaml
git commit -m "feat(agent): add AGENT_READ_ONLY env var to deployment"
```

---

## Task 3: Build, Import, and Test

### Step 1: Build Docker image

```bash
cd terraria-agent && docker build -t terraria-agent:latest .
```

### Step 2: Import to k3s

```bash
docker save terraria-agent:latest | sudo k3s ctr images import -
```

### Step 3: Test with read-only disabled (default)

```bash
sudo k3s kubectl rollout restart deployment/terraria-agent -n terraria
# Test: command should execute normally
```

### Step 4: Enable read-only and test

```bash
sudo k3s kubectl set env deployment/terraria-agent AGENT_READ_ONLY=true -n terraria
# Test: command should be blocked, narration still works
```

### Step 5: Commit all changes

```bash
git add -A
git commit -m "feat(agent): implement read-only mode with env var toggle"
```
