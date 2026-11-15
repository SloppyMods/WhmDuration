# Verification evidence

## Core

Command: `/home/you/.dotnet/dotnet run --project tests/WhmDuration.Tests` from WhmDuration.

- Red phase: 26/57 passed, exit 1 against unimplemented contracts. Missing mappings, timers, target selection and tooltip breakdowns failed.
- First implementation: 56/57 passed. Revealed float truncation producing 39 instead of 40 for 18 seconds remaining of 30.
- Corrected intermediate calculation to double using elapsed time: 57/57 passed, exit 0.

Covered: all six WHM action/status mappings, source ownership and source sentinels, expiry/removal, refresh without retained timers, finite-value handling and clamping, GCD priority, target selection policy, full-duration potency arithmetic, trait-adjusted text inputs, description preservation, idempotence, invalid/unsupported descriptions.

## Installed API evidence

.NET SDK: `/home/you/.dotnet/dotnet`, version 10.0.401. Not available on default PATH.

Dalamud SDK 15.0.0 defaults to `/home/you/.xlcore/dalamud/Hooks/dev`. Inspected installed FFXIVClientStructs metadata using dnlib:

- `AddonActionBarBase.Delegates.UpdateHotbarSlot` returns void and accepts addon, slot, number array, string array and both array indices.
- `ActionBarSlotNumberArray` is 17 int-sized entries (68 bytes). Type is index 0, ActionId index 3, Executable indices 5/6, MaxCharges index 7, GCD percent index 8, charge cooldown percent index 9, RechargeTime/ManaCost index 10, CurrentCharges index 13.
- UI intermediate SlotType.Action = 48; this is not ActionType.Action = 1.
- `AgentActionDetail` exposes ActionKind, ActionId, OriginalId, AdjustedId and the number/string arrays. DetailKind.Action = 30.
- `StringArrayData.SetValue` supports a ReadOnlySpan<byte> and managed copying.

Metadata alone did not establish rendering semantics. A read-only signature scan and Iced disassembly of the installed `/home/you/.xlcore/ffxiv/game/ffxiv_dx11.exe` established:

- UpdateHotbarSlot signature had exactly one match, resolving to VA 0x141585B60.
- It calls slot renderer 0x141119350, which calls text renderer 0x1411193D0 and icon renderer 0x141119B20.
- Text renderer reads selector index 2. Jump-table mode 3 routes to 0x14111977A, reads numeric seconds from index 10, and uses index 12 for text visibility.
- Icon renderer switches on index 7 values 0/1/2/3 as cooldown display modes. **The installed field name MaxCharges is misleading here.** Mode 0 calls native drag/drop cooldown renderer with progress from index 8. Native drag/drop renderer 0x1406D0A70 accepts active sweep percentages 1–99 and treats 100 as complete.
- Tooltip renderer signature also had exactly one match, resolving to VA 0x1412E0710.

These addresses are evidence for this local binary only; the plugin resolves signatures at runtime and does not hardcode them. Actual visual appearance still requires an in-game check.

## Runtime availability

A running `ffxiv_dx11.exe` process was observed. Its executable is `/home/you/.xlcore/ffxiv/game/ffxiv_dx11.exe`, and the launcher arguments identify Dalamud `Hooks/15.0.3.6` as the active runtime (not merely `dev`). No game interaction, plugin deployment, or live visual checks have been performed.

## Native acceptance (not yet tested)

- [x] Plugin builds and packages against installed API 15.
- [ ] Own Dia displays sweep/seconds on standard hotbars and remains castable.
- [ ] Early refresh resets; dispel, expiry and enemy switching clear/select effects correctly.
- [ ] Other WHM effects are ignored.
- [ ] Regen follows selected friendly target, or self when targeting an enemy.
- [ ] Medica II/III tracks own HoT, not a party aggregate.
- [ ] Level-synced Aero/Aero II and Medica upgrades work.
- [ ] Real GCD takes precedence.
- [ ] Native tooltip retains original payloads and reflows for the appended total.
- [ ] Repeated hover does not duplicate the appended section.
- [ ] Toggle/unload restores native visuals and tooltip.

## Native adapter build and unit evidence

- Added real unmanaged-array tests for temporary native UI inputs, null/bounds/non-action guards, executable-flag preservation, exactly one renderer invocation, and restoration when rendering throws. Red phase: 65/67 passed, exit 1. Green phase: 67/67 passed, exit 0.
- `DALAMUD_HOME=/home/you/.xlcore/dalamud/Hooks/15.0.3.6 /home/you/.dotnet/dotnet build src/WhmDuration.Plugin -c Release`: succeeded, 0 warnings, 0 errors, exit 0.
- Verified generated `WhmDuration/latest.zip` includes WhmDuration.dll, WhmDuration.Core.dll, WhmDuration.deps.json and WhmDuration.json. Manifest declares API 15 and internal name WhmDuration.
- Hotbar adapter rerenders bound slots in PostUpdate, using live game-produced number/string indices and freshly obtained arrays; it does not retain native slot pointers across frames.
- Native tooltip adapter preserves SeString payloads, uses game-owned managed string storage, and restores the original array string after rendering.

## Independent review

Read-only reviewer inspected the plugin/core/tests and independently ran the Release test DLL: 67/67, exit 0. One issue was found and fixed: temporary tooltip writes must use suppressUpdates=true so replacement/restoration do not mark the string array dirty or request unrelated addon refreshes. Confirmed against the installed SDK contract; no infinite update loop was claimed or observed.

After that fix, reran the full Release test command, plugin build against active Hooks/15.0.3.6, and ZIP/manifest validation: 67/67 tests, 0 compiler warnings/errors, API 15 package validated, combined exit 0.

## Live registration handoff

User approved “Register for testing” through Q4. Native desktop automation could not start: `orca-ide skills get computer-use --json` exited 127 with `bash: line 1: orca-ide: command not found`. No alternate desktop executable was attempted. No game settings were edited, no plugin was loaded, and the running game was not interacted with. Editing the on-disk Dalamud config while its live process owns that config was not used as a substitute for UI registration.

Manual dev-plugin registration uses the Wine-visible DLL path documented in README.md. Await user confirmation of loading, then verify runtime logs and native behavior. The plugin is implemented and packaged, but has not been loaded or visually tested in game. The goal remains active.

## Installed client data and trait evaluation

Read the actual local sqpack data through the installed Lumina/Lumina.Excel assemblies (read-only disposable inspector; exit 0). Confirmed all six action names, action levels, status IDs/names, and effect durations against this installation, rather than only the external XIVAPI snapshot.

Actual ActionTransient descriptions include conditional SeString macros:
- Dia: job global parameter 68 is WHM (24), level global parameter 72 >= 94 selects 85 potency; otherwise 65. This applies to both initial and periodic potency.
- Regen: WHM level >= 85 selects 250 periodic potency; otherwise 200.
- Medica II: WHM level >= 85 selects 250 initial/150 periodic; otherwise 200/100.
- Aero/Aero II and Medica III descriptions have literal potency values in this client.

Raw text extraction drops conditional-expression values. Whether the native description array is already evaluated at our hook boundary has not been observed live. Made evaluation explicit with the SDK ISeStringEvaluator before parsing; retained the original SeString payloads for native rendering. Already-evaluated text passes through that evaluator normally. SDK contract verified at Dalamud.xml:63284–63300. Runtime/global-state evaluation still requires a loaded game plugin; a successful build does not establish it.

Added literal lower-level input cases: Dia total 715, Regen 1200, Medica II 700. Current Release tests: 70/70 passed; active-runtime plugin build: 0 warnings/errors, combined exit 0.

Successful adapter initialization and `/whmduration status` now emit plugin-scoped log entries as well as chat diagnostics, allowing the next check to observe real native call counts. Initial search of the runtime log directory in goal round 3 found no WHM Duration entries. No plugin registration or game interaction was performed by the agent.

## First observed runtime result

User manually loaded the plugin and ran `/whmduration status`. Independently found the same status in the ACT network log at `Network_30301_20261003.log:213565` (2026-10-04T10:52:09.438-04:00): enabled, hotbar fault=False, tooltip fault=False, native renders=5644760, overlays=0, tooltip additions=1.

This confirms the command ran, the native renderer hook was invoked, and one tooltip substitution occurred without a reported adapter fault. It does not establish visual appearance or prove cooldown overlays work: the overlay count is zero. The user was asked to cast self-Regen and check the button after the normal GCD finishes.

Goal round 4 read the log region after that status. No second WHM Duration status or self-Regen test result was observed there. Runtime testing remains pending. No overlay defect was asserted from the idle zero count alone.

## Active self-Regen failure (F2)

User answered Q6 “No countdown” with own Regen active after the normal GCD ended. Independently observed a new ACT status-add record at Network_30301_20261003.log:214046, 2026-10-04T10:59:41.341-04:00: status 0x9E (158, Regen), 18 seconds, identical source/target IDs belonging to the local player. This is a real runtime failure of the requested hotbar behavior, not evidence that the pure model is wrong.

Read-only native recast-gate disassembly resolves IsRecastTimerActive to VA 0x1408EE870, which delegates to 0x1408EE900. It consults actual recast groups/charge state, rather than a guessed always-on gate. No recast-gate patch was made without live values.

Added `/whmduration diag` to capture, on demand, local job/PvP state, self Regen status source and remaining time, target selection, selected duration, native recast active/elapsed/total, computed visual, recorded hotbar bindings, and actual slot UI type/array action/native slot action. This does not modify rendering, status, casting, or cooldown behavior. The purpose is to locate the failing boundary before choosing a fix. Diagnostic build verified: 70/70 Release tests, 0 build warnings/errors, exit 0. Await a diagnostic capture while Regen remains active. The user's next status showed render count reset to 15984 but still overlays=0. ACT records show self-Regen applied at 11:06:36.300 and the status at 11:06:39.929, confirming the persistent failure after reload. No `WHM Duration diag` lines were present.

Read-only registration check: Dalamud config enables automatic reloading and registers the C:\users\SloppyMods\Desktop alias of the build DLL. Resolved the Wine alias to the actual current workspace DLL and compared SHA-256: identical. No config was changed. This excludes a wrong registered file path, but does not prove which assembly revision is already in memory.

Goal round 5 still had no diagnostic capture. Registered sentinel watch-1 to wake this session when `WHM Duration diag` appeared in the recent ACT network logs (10-second read-only probe, one-shot, 24-hour expiry). Goal was subsequently blocked pending that evidence.

## F2 root cause and targeted patch

User supplied `/xllog` diagnostic records from 11:40:02 and 12:14:15. All seven visible standard addons (_ActionBar, _ActionBar01–05, _ActionBar09) reported 12 slots, vector count 12, cross=True, and no bindings. Available=True, job=24, PvP=False. Both captures had no self-Regen, so they do not validate active status selection or the recast gate.

The HotbarCooldowns.Detour and Redraw eligibility guards independently rejected bars when `IsCrossHotbar` was true. This explains why standard bars never acquired bindings: the native flag cannot serve as an exclusion predicate for this client's actual standard addons. Exact standard addon names remain the reliable scope boundary. No claim is made about why the vendor field reports true.

Resumed the goal after this user-provided evidence. Extracted the existing scope predicate into the pure UI override boundary and reproduced rejection with the observed flag input. Regression red phase: 98/108 passed, exit 1; exactly ten supported names with cross=True failed. Removed flag-based rejection and changed both native call sites to the tested exact-name predicate. Unsupported/cross-hotbar names remain rejected regardless of the reported flag.

Fresh verification: 108/108 Release tests passed; plugin build against active Hooks/15.0.3.6 succeeded with 0 warnings/errors, combined exit 0. No recast/status/target changes, action interception, or cooldown-state writes were added. User selected Q8 “Countdown works” after the patched build's active self-Regen check. This is user-observed evidence that the native sweep/remaining seconds now appear after the GCD. No post-patch counter trace or screenshot was supplied, so do not describe this as an independently observed visual test. Dia, Medica III, native tooltip totals, and remaining native acceptance cases still need validation.

Attempted to cancel the superseded watch; runtime reported no active subscription named watch-1. No replacement watch was created. The supplied `/xllog` records were not visible in the ACT log used by that watch, so future runtime checks should use the user's `/xllog` capture rather than assuming all diagnostic output appears in ACT.

## Approved D4 countdown text controls

User requested countdown text location/size options and approved D4 through Q10. Added a saved configuration window through both plugin UI buttons and `/whmduration config`, retaining the existing no-argument toggle command. Controls: text size 50–300% of native size, X/Y icon-local offsets ±100 px, live updates and reset to native defaults. Scaling the native text node preserves the existing game font/proportions; no font atlas or action behavior is changed.

Added pure normalization/layout/ownership tests before implementation. Red phase: 114/124 passed, exit 1. Green phase: 124/124 passed, exit 0. Verified defaults, clamps and nonfinite input, relative geometry, original-layout restoration exactly once, replaced-node identity protection, addon finalization, and no cumulative transform over repeated frames.

Native adapter reacquires the actual countdown text node from the current slot. It stores original layout scalars plus identity for comparison, never a saved pointer to dereference later. It restores previous layout before the original renderer, applies custom styling after successful duration rendering, forgets finalized addons, and restores hidden ready bars on disable/unload. Configuration loads with compatible defaults and sanitization; changes/reset save through the normal Dalamud plugin configuration API.

Release tests: 124/124 passed. Release build against active Hooks/15.0.3.6: 0 warnings/errors, combined exit 0. Focused independent read-only review found no evidence-backed important defects in layout lifetime/restoration, persistence or ImGui Begin/End/event lifetime. Reviewer's test rerun failed due to missing dotnet on their PATH, so the successful test/build evidence is the captain's run, not an independent execution.

Pending live D4 acceptance: settings window opens, size/offset changes are visible while Regen is active, reset returns the normal appearance, preferences survive reload, and `/whmduration off`/unload restores the normal layout. Dia/Medica III and native tooltip totals remain unverified visually in the recorded evidence. User selected Q11 “Not tested yet”.

Goal round 9 made a separate read-only verification of the actual built PluginConfig type with the installed Newtonsoft.Json assembly. Command: `/home/you/.dotnet/dotnet run --project /tmp/whmduration-config-check -c Release`. Result: 10/10 checks passed, exit 0. Verified initializer defaults, fields serialized as Version/TextOffsetX/TextOffsetY/TextSize (computed Style excluded), non-default values surviving serialization/deserialization, missing-field compatibility defaults, clamp behavior, reset round-trip, and sanitization of nonfinite inputs. The check did not create or edit the game's configuration; it proves serialization behavior, not live Dalamud save/reload or visual acceptance.

## Remaining live acceptance handoff

Across goal rounds 8–10, no new native visual acceptance was supplied after Q11 “Not tested yet”. In round 10, checked again: orca-ide remains unavailable on PATH. Implementation, 124 unit tests and the separate 10 actual-config serialization checks are complete; original-goal native acceptance is still partial (user-confirmed Regen countdown and logged tooltip additions). Do not mark the full objective complete based only on those tests.

Need user checks: Dia and Medica III countdowns and native tooltip totals; the saved text controls visibly resize/move the active countdown; reset/default appearance, reload persistence and disable cleanup. No further speculative changes are needed before these checks. Await the user's next testing instruction/result rather than repeating autonomous build/status rounds.

## All-Job PvE and Ground-Effect Expansion (D5 / Q13–Q18)

Expanded scope from WHM to all PvE jobs (Tanks, Healers, Melee, Phys Ranged, Casters, BLU) and oGCD ground effects:
- Enabled `GameEffects.Available` across all PvE classes/jobs.
- Implemented `GroundRecast` dual-phase timer logic in `DurationModel.GetGroundVisual`: active duration countdown while `elapsed < duration`, smoothly falling back to native real cooldown while `elapsed >= duration`.
- Added catalog entries for PLD, WAR, DRK, GNB, DRG, NIN, SAM, BRD, MCH, BLM, SMN, BLU, WHM, SCH, AST, SGE.
- Generalized `PotencyTooltip` to parse evaluated action text across all jobs, with initial + periodic totals and `per recipient` tags for multi-target heals.
- Unit tests expanded from 124 to 156 tests covering catalog entries, GroundRecast timing phases, and multi-job tooltip evaluations.
- Full Release build: **156/156 tests pass**, **0 compiler warnings/errors**, package verified with manifest and core DLL.

