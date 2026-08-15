# Task 5 Report: `ChatController` — validación y guardia en Ruta 3

**Status:** DONE_WITH_CONCERNS
**Commit:** `126f6b0` `feat(agent): validate and guard Groq actions in chat route`
**Branch:** `feature/agent-hardening` (worktree)

## What I implemented

Applied the brief's three changes to `terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs`:

1. **Step 1 — fields + constructor**: Added `_actionValidator` / `_onlinePlayers` fields, `ActionValidator actionValidator` / `OnlinePlayersService onlinePlayers` constructor params, and `_actionValidator = ...; _onlinePlayers = ...;` assignments after `_history = history;`. (Verbatim.)
2. **Step 2 — Route 3 guard block**: Replaced the "Execute TShock action if detected" block (lines 319-352) with the brief's guarded version: `ActionValidator.Validate()` gate, offline-target rejection via `OnlinePlayersService.GetSnapshotAsync()`, `give` routing to `HandleGiveAsync`, `maxhp` routing via `IsMaxHpAction`, generic-failure honest narration, and mechanical-command narration replacement (`IsMechanical` + `IsGenericNarration`). (Verbatim, except one line — see Deviation below.)
3. **Step 3 — helpers**: Added `MechanicalCommands`, `IsMechanical`, `IsGenericNarration` at the end of the class after `HandlePeligro`. (Verbatim.)

No other logic, methods, strings, or formatting were touched. Only `ChatController.cs` changed (82 insertions, 12 deletions).

## Deviation from the brief (justified)

Brief's line 141: `executedAction = validation.Action;`
Applied as: `executedAction = validation.Action ?? string.Empty;`

**Why:** The brief's verbatim code produces `warning CS8604` because `executedAction` is inferred as `string?` (from `intent.Action`, a `string?`) and then reassigned from `validation.Action` (also `string?`), so the compiler's flow analysis cannot prove non-null at the three call sites that require `string` (`IsMaxHpAction`, `HandleMaxHpAsync`, `TShockClient.ExecuteCommandAsync`). With the brief verbatim, the build reports 1-3 CS8604 warnings. My dispatch requires 0 warnings.

The `?? string.Empty` on that single reassignment restores the compiler's NotNull flow state for `executedAction` within the `else` block, eliminating all warnings with **zero behavioral change**:
- The reassignment only executes when `validation.IsValid` is true, and the validator (`ActionValidator.cs` lines 241-287) always sets `Action` non-null for valid results, so `?? string.Empty` never triggers.
- When `intent.Action` is null/empty/whitespace, the whole block is skipped and `executedAction` retains `intent.Action` (null), so the response's `action` field is `null` — identical to the previous behavior.

I first tried making `IsMaxHpAction(string?)` nullable-aware, but that merely exposed two further CS8604 warnings downstream; the single-line `?? string.Empty` is the minimal deviation that achieves 0 warnings with identical semantics. The unit tests do not assert on `ChatController`'s response body, so this cannot affect the 48/48 result.

## Evidence

### Build (pristine, worktree-mounted SDK image)
```
docker run --rm -v /home/roman/k8s-projects/.worktrees/agent-hardening/terraria-agent/src:/src -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet build Terraria.Agent.Api/Terraria.Agent.Api.csproj -c Release
```
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Test (full suite)
```
docker run --rm -v /home/roman/k8s-projects/.worktrees/agent-hardening/terraria-agent/src:/src -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet test Terraria.Agent.Api.Tests/Terraria.Agent.Api.Tests.csproj -c Release
```
```
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48
```

## Files changed

- `terraria-agent/src/Terraria.Agent.Api/Controllers/ChatController.cs` (only file, staged and committed)

## Self-review findings

- **Completeness:** All four steps of the brief implemented (fields/ctor, guard block, helpers, build/test/commit).
- **Quality:** Code matches the brief verbatim except the one documented line (`?? string.Empty`); no stray refactors.
- **Discipline (YAGNI):** Nothing added beyond the brief; no comments in the new code.
- **Integrity:** `git diff` confirmed the change touches only the intended regions (fields block, constructor, Route 3 execution block, new helpers at end of class). No reformatting, reordering, or renames of existing methods/strings.
- **Testing:** Build 0 warnings / 0 errors; tests 48/48 pass; both run in a pristine container with the worktree source mounted.

## Issues or concerns

1. **CS8604 vs verbatim fidelity:** The brief's verbatim code does not compile warning-free. I fixed it with a minimal, semantically-neutral single-line change rather than asking for clarification, because the task explicitly demands 0 warnings and the fix is provably behavior-preserving. If strict verbatim fidelity is preferred over 0 warnings, this line can be reverted (`executedAction = validation.Action;`).
2. **Not yet covered by tests:** The new Route 3 guard behavior (invalid-action rejection, offline-target rejection, give routing, mechanical narration replacement) has no unit tests — the current 48 tests cover only `ActionValidator`, `GroqRateLimiter`, and `OnlinePlayersService`. A future task may want controller-level tests for the guard.
