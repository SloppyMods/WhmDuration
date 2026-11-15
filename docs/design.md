# WHM Duration design

Approved in chat: existing hotbar buttons, WHM first, display-only duration cooldowns and full-duration potency in the native tooltip.

## Scope

New standalone Dalamud API 15 plugin. Standard hotbars, directly placed Aero (121), Aero II (132), Dia (16532), Regen (137), Medica II (133), Medica III (37010). No changes to action execution, cooldown state, packets, macros, cross-hotbars, or other plugins. No Asylum support.

## Behavior

Read live status remaining time, restricted to the local player's source ID. Damage statuses follow the selected enemy. Regen follows a friendly target, or self when the selected target is hostile or absent. Medica follows self, serving as an indicator rather than claiming all party members still have the effect. The game's real recast display wins while active. Early refresh, target switch, effect removal and expiry follow live statuses without retained timers.

## Native integration

Hook AddonActionBarBase.UpdateHotbarSlot, restricted to standard bar names and action-type slots. Temporarily substitute UI-only cooldown fields for the original renderer; restore the number array in finally. Do not modify executable flags, ActionManager state, or hotbar action IDs. Validate indices and pointers before access. Hooks fail closed on exceptions and report the fault once.

Hook native action tooltip generation before its renderer. Read description string index 13 as a SeString, preserve all existing payloads, and append a plain-text potency breakdown. Explicitly evaluate description macros with Dalamud's ISeStringEvaluator for parsing, and preserve the original SeString payloads for rendering. Compute potency from the evaluated English description, rather than duplicating potency tables and trait logic. Unsupported language or unfamiliar descriptions leave the original tooltip unchanged. Make augmentation idempotent. The total assumes the full effect runs with a 3-second periodic tick; it is nominal potency, not guaranteed HP damage, effective healing, or a party aggregate.

## Verification

A dependency-free net10.0 test runner checks status ownership, targets, expiry, refresh, invalid times, cooldown clamping and priority, known tooltip forms, unsupported descriptions, and idempotence. Compile against the installed Dalamud/FFXIVClientStructs assemblies. Native visual semantics, tooltip reflow, unload restoration, and actual current-game compatibility require in-game checks and cannot be inferred from a successful build.

## Sources

- Installed Dalamud SDK 15.0.0 and Hooks/dev assemblies.
- https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Arrays/Common/ActionBarSlotNumberArray.cs
- https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Tweaks/TooltipTweaks.cs (tooltip generation signature and description index 13).
- https://v2.xivapi.com/api/sheet/ActionTransient?rows=121,132,137,133,16532,37010&fields=Description
- https://v2.xivapi.com/api/sheet/Status?rows=143,144,158,150,1871&fields=Name,Description
- https://v2.xivapi.com/api/search?sheets=Status&query=Name%3D%22Medica%20III%22&fields=Name,Description

No source code is copied from another plugin.

## Initial limitations

English tooltip descriptions only. Native hooks must be checked after game updates. Other UI plugins touching the same hotbar or tooltip may conflict. Third-party plugins violate FFXIV's rules.
