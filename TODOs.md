# TODOs

## Features

- Grav Reactor/installed-corpse destruction should respawn grav core
- Scale grav refill fraction + Grav Overcharge hours by gravship trip distance
  (deferred from the Grav Overcharge build; straight-line tiles between the
  controller's takeoff/landing tiles, normalization curve + reference-distance knob)

- Add Persona Core upgrade part (increase consciosness +10%, increase threshold to awaken?)

- Add setting-gated workgiver to autoqueue reactor replacements on low-power androids?
- Add setting sliders for low-power alert/auto-replacement thresholds? PR upstream?

- Eject Thanatic reactor on corpse destruction?
- Extract Thanatic reactor gizmo on corpse.
  `claude --resume "thanatic-reactor-designation-ui"`

- Magus of the Library scenario
  - Vanometric reactor
  - Green armor
  - Archotech arm
  - 7 books
  - Custom backstory

- Verify Magus of the Abyss in the UI (scenpart defs?)
  - Set custom backstory

- Only fire/scale thanatic drain on psychically sensitive targets
- Add powerfocus chip to reactor recipes? Evaluate recipe costs
- more exclusion tags for conflicting subroutines
- When sleep mode is used by an android with a vanometric reactor installed, building acts as a 500w power emitter

## Other?

- Android stands aren't pawn-bound and androids with low memory will path to and use the nearest

## Cleanup

- Review def descriptions copy
- Keyed language strings everywhere
- Check patches for upstream issues and file reports/PRs
- Neutro infusion operation upstream feature PR?

## Localization

- When translations are eventually added, MayRequire-gated defs' DefInjected
  entries must ship from their own LoadFolders-gated compat root, not the
  main `1.6` tree — DefInjected ignores MayRequire, so the folder is the only
  gate, and a main-tree entry is a found-no-def startup error whenever the
  gate is inactive (see BetterTradersGuild's Biotech ScenPartDef move,
  commits d9af1f0/7de4368/4be8e8b, for the pattern). Per gate:
  - Odyssey (`1.6/Mods/Odyssey/Languages/<Language>/...`): `AAH_GravReactor`
    (`GeneDef` via the `VREAndroids.AndroidGeneDef` alias, `HediffDef`,
    `ThingDef`), `AAH_GravOvercharge` (`HediffDef`), `AAH_GravChargeAura`
    (`ThingDef`, a mote), `AAH_MakeGravReactor` (`RecipeDef`),
    `AAH_InstallGravReactor` (`RecipeDef`)
  - Anomaly (`1.6/Mods/Anomaly/Languages/<Language>/...`):
    `AAH_MakeThanaticReactor` (`RecipeDef`)
  - VFEPower (`1.6/Mods/VFEPower/Languages/<Language>/...`, a workshop mod
    not a DLC): `AAH_SalvageThanaticReactor` (`RecipeDef`)
  - `AAH_InstallThanaticReactor` (`RecipeDef`) is NOT gated and stays in the
    main tree.
  - Scanned for commented-out/excluded DefInjected entries as part of this
    same pass: none exist today (no DefInjected content ships yet at all).
