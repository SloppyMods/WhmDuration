# WHM Duration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Display actual WHM DoT/HoT duration as cooldowns on native standard-hotbar buttons and append full-duration potency to native tooltips without changing casting.

**Architecture:** A pure core selects owned live statuses and computes display fields and potency breakdowns. A thin Dalamud plugin adapts game targets/statuses and temporarily overrides only the renderer's UI arrays. Native rendering stays the game's responsibility.

**Tech Stack:** C#/.NET 10, Dalamud.NET.Sdk 15.0.0, installed FFXIVClientStructs, dependency-free console tests.

**Spec:** design.md

## Global Constraints

- API 15; standard hotbars only; directly placed actions only.
- Actions 121, 132, 16532, 137, 133, 37010 only.
- No ActionManager recast writes, action interception, network hooks, or executable flag changes.
- Native cooldowns take priority. Statuses must be owned by the local player.
- Unsupported language or description leaves tooltip unchanged.
- No in-game success claim without observed in-game tests.

---

### Task 1: Live-status and potency core

**Files:** Create src/WhmDuration.Core/WhmDuration.Core.csproj, DurationModel.cs, PotencyTooltip.cs; tests/WhmDuration.Tests/WhmDuration.Tests.csproj, Program.cs.

**Interfaces:** `StatusSnapshot(uint StatusId, uint SourceId, float RemainingSeconds)`; `EffectDefinition(uint ActionId, uint StatusId, float DurationSeconds, EffectTarget Target)`; `DurationModel.SelectRemaining(EffectDefinition, uint, IEnumerable<StatusSnapshot>)` returns float; `DurationModel.GetVisual(float remaining, float duration, bool realCooldown)` returns nullable `CooldownVisual(int ProgressPercent, int Seconds)`; `PotencyTooltip.Append(uint actionId, string description)` returns original or augmented string.

- [x] Create tests that assert owned Dia at 18 seconds yields progress 40 and seconds 18, other sources and expired effects yield no overlay, and native cooldown takes priority.
- [x] Create tests for target resolution, invalid floats, oversized durations, early refresh and expiry.
- [x] Create tooltip cases for Dia 85 + 10*85 = 935, Aero 50 + 10*30 = 350, Regen 6*250 = 1500, Medica II 250 + 5*150 = 1000 per recipient, Medica III 250 + 5*175 = 1125 per recipient. Unknown actions/description forms must be unchanged.
- [x] Run `/home/you/.dotnet/dotnet run --project tests/WhmDuration.Tests`; verify failure before implementation.
- [x] Implement the core using owned status snapshots, finite/clamped time, ceiling seconds, and validated English-description parsing; run the tests to green.

### Task 2: Native renderer adapter

**Files:** Create src/WhmDuration.Plugin/WhmDuration.Plugin.csproj, Plugin.cs, GameEffects.cs, HotbarCooldowns.cs, NativeTooltips.cs.

**Interfaces:** Core from Task 1. `GameEffects.GetRemaining(uint actionId)` returns float; `HotbarCooldowns` and `NativeTooltips` implement IDisposable and own hooks.

- [x] Inspect actual installed assembly metadata for hook signatures, enum values and array fields before implementing adapter assumptions.
- [x] Add real unmanaged-array tests for temporary replacement/restoration, including exception paths, out-of-bounds indices, non-actions and active native cooldowns; observe red before implementation.
- [x] Hook UpdateHotbarSlot using generated delegate type. Validate addon name, slot type, action, number-array size, local player WHM/CNJ state and game UI visibility. Read targets and owned statuses using Dalamud services. Temporarily write progress and remaining seconds to UI renderer inputs only and restore them in finally.
- [x] Hook action tooltip generation with inspected signature; use AgentActionDetail for adjusted action ID and preserve SeString payloads when appending index 13. Unsupported input passes through.
- [x] Register `/whmduration` to toggle off/on and print enabled/fault state. Dispose hooks and restore UI on unload.
- [x] Run core tests and `/home/you/.dotnet/dotnet build src/WhmDuration.Plugin -c Release`; resolve all compiler errors and warnings.

### Task 3: Package and acceptance evidence

**Files:** Create README.md; native-test checklist and observed build/test evidence in docs/verification.md.

- [ ] Explain build/install, command, supported actions, English-only tooltip behavior, potency assumptions, and ToS risk.
- [ ] Run the Release tests and build again, record commands and results.
- [ ] Verify in game: Dia sweep/countdown, enemy switching, another WHM's effect ignored, early refresh, Regen target/self fallback, Medica self indicator, synced Aero and Medica upgrades, native GCD priority, no cast blocking, tooltip repeated hover/resize, toggle/unload cleanup.
- [ ] If there is no accessible game session, mark native acceptance unverified. Leave the goal active rather than claiming full completion.
