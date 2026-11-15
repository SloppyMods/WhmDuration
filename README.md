# WHM Duration (All-Job DoT/HoT Tracker)

Experimental Dalamud API 15 plugin: duration-based cooldown displays on existing hotbar buttons across all PvE jobs, plus full-duration potency in native English action tooltips.

**Build/package verified. In-game acceptance verified for WHM Regen; broader job verification in progress. Native hooks can break after game updates. Third-party plugins violate FFXIV's rules.**

## Supported Spells & Abilities

- **White Mage**: Aero, Aero II, Dia, Regen, Medica II, Medica III, Asylum, Liturgy of the Bell.
- **Scholar**: Bio, Bio II, Biolysis, Baneful Impaction, Sacred Soil, Whispering Dawn.
- **Astrologian**: Combust I/II/III, Aspected Benefic, Aspected Helios, Helios Conjunction, Celestial Opposition, Celestial Intersection, Collective Unconscious, Earthly Star.
- **Sage**: Eukrasian Dosis I/II/III, Eukrasian Dyskrasia, Physis I/II, Kerachole.
- **Tanks**: Circle of Scorn, Blade of Valor (PLD), Equilibrium (WAR), Salted Earth (DRK), Sonic Break, Bow Shock, Aurora (GNB).
- **Melee DPS**: Chaos Thrust, Chaotic Spring (DRG), Doton (NIN), Higanbana (SAM).
- **Physical Ranged DPS**: Caustic Bite, Stormbite, Windbite, Venomous Bite (BRD), Bioblaster (MCH).
- **Casters & Blue Mage**: Thunder I–IV, High Thunder I/II (BLM), Slipstream (SMN), Song of Torment, Aqua Breath, Nightbloom, Breath of Magic (BLU).

### Dual-Phase Ground Effect Timers (Asylum, Sacred Soil, Salted Earth, etc.)
- While active (`elapsed < duration`): button shows active duration countdown and sweep.
- When expired (`elapsed >= duration`): button smoothly switches to remaining real cooldown until ready.

Soft targeting takes priority over hard target. PvE only. Standard hotbars, directly placed spells only. Real recasts/GCD take priority over effect timers.

Tooltips preserve the original description and append initial, periodic, and full-duration total potency (labeled per recipient for AoE heals). Potency is parsed from the evaluated English tooltip, so unsupported languages/description formats remain unchanged.

## Build and test

Requires .NET 10 and Dalamud SDK/API 15 reference assemblies.

```sh
/home/you/.dotnet/dotnet run --project tests/WhmDuration.Tests -c Release
DALAMUD_HOME=/home/you/.xlcore/dalamud/Hooks/15.0.3.6 \
  /home/you/.dotnet/dotnet build src/WhmDuration.Plugin -c Release
```

The default SDK reference path is your launcher's `Hooks/dev` directory. `DALAMUD_HOME` selects the exact runtime being used by the game. Building does **not** install or reload the plugin.

Output DLL: `src/WhmDuration.Plugin/bin/Release/WhmDuration.dll`.

Package: `src/WhmDuration.Plugin/bin/Release/WhmDuration/latest.zip`. It includes the core DLL dependency and generated manifest.

## Load locally for testing

Use a safe area, not a duty or active combat, for the initial load.

1. Open Dalamud settings with `/xlsettings`.
2. In the Experimental tab, add the full path to `WhmDuration.dll` to Dev Plugin Locations. For this Linux/Wine installation, use the Windows-visible path:
   ```text
   Z:\home\SloppyMods\Projects\ffxiv-tools\WhmDuration\src\WhmDuration.Plugin\bin\Release\WhmDuration.dll
   ```
3. Open `/xlplugins`, use the Dev Tools tab to rescan if necessary, and enable WHM Duration.
4. Place the supported spells directly on a standard hotbar and run `/whmduration status`.
5. Run the native checks in `docs/verification.md` before relying on it.

Do not copy only the plugin DLL: keep `WhmDuration.Core.dll` and `WhmDuration.json` beside it. Disable/unload the plugin through Dalamud to remove its hooks. `/whmduration off` restores current native visuals while leaving the plugin loaded.

## Countdown text settings

Open the plugin's settings button or `/whmduration config`.

- **Text size:** 50–300% of the game's original countdown text size. Uses native node scaling, preserving the game font and existing scale proportions.
- **Horizontal/vertical offset:** -100 to +100 icon-local pixels from the original position.
- **Reset to defaults:** 100% size and zero offsets.

Changes apply live to active supported countdowns and save automatically through Dalamud's plugin configuration. Defaults leave the original layout unchanged. Original text layout is restored before each native render, when the effect/overlay ends, and on plugin disable/unload; replaced/finalized nodes are not restored through stale pointers. Native settings behavior still requires an in-game check.

## Commands

- `/whmduration`: toggle.
- `/whmduration on` or `off`: explicitly enable or disable.
- `/whmduration config`: open saved countdown text size/position settings.
- `/whmduration status`: print enabled/fault state and native render/overlay/tooltip counts.
- `/whmduration diag`: one-shot status/target/recast and actual hotbar-slot input trace for investigating a missing countdown. Prints to chat and the plugin log.

If either adapter reports a fault, it stops adding its changes and logs the error once. Reload only after inspecting the error. The native arrays are restored after every patched render, including exception paths; no real action cooldowns or executable flags are written.
