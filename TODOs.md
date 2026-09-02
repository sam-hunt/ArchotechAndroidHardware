# TODOs

## Pre-release features

All of these are in scope for 1.0.0. Items marked **(decision)** need a design
call before implementation; see RELEASE_READINESS_AUDIT.md for the open questions.

- Archotech android xenotype **(decision)**
- Persona Core upgrade part (increase consciousness +10%, increase threshold to
  awaken?) **(decision)** — gene icon `Gene_PersonaCore.png` already shipped;
  `displayOrderInCategory` gaps at 1/6/8 are reserved for it
- Magus of the Abyss
  - Verify in the UI (scenpart defs, `VREA_Eyes_Red` implied-def reference)
  - Set custom backstory **(decision)** — no vanilla backstory ScenPart exists;
    options are a bespoke `ScenPart` or a dedicated `PawnKindDef` with
    `backstoryFiltersOverride` (VREA ships `BackstoryDefs/Android_Awakened.xml`)
- Magus of the Library scenario **(decision)**
  - Vanometric reactor, green armor, archotech arm, 7 books, custom backstory
- Reactor removal **(decision)** — VREA's generic `VREA_RemoveArtificialPart`
  ("remove component") already applies to the reactor slot, and on that path
  Thanatic/Grav spawn no item (no `spawnThingOnRemoved`; `EjectCustom` only
  runs from the install patch), so the reactor is lost. Either give the
  reactors `PostRemoved`-side ejection, or ship explicit `AAH_Remove*` recipes
  and block VREA's generic one on our slot. Needs an in-game repro first.

## Post-release features

- Grav Reactor/installed-corpse destruction should respawn grav core
- Scale grav refill fraction + Grav Overcharge hours by gravship trip distance
  (deferred from the Grav Overcharge build; straight-line tiles between the
  controller's takeoff/landing tiles, normalization curve + reference-distance knob)
- Add setting-gated workgiver to autoqueue reactor replacements on low-power androids?
- Add setting sliders for low-power alert/auto-replacement thresholds? PR upstream?
- Eject Thanatic reactor on corpse destruction?
- Only fire/scale thanatic drain on psychically sensitive targets
- Add setting for refill per non-humanlike kill (mechs, animals, entities)
- Add powerfocus chip to reactor recipes? Evaluate recipe costs (Thanatic/Grav
  craft at Crafting 8 vs Vanometric 14 and the implants at 12)
- More exclusion tags for conflicting subroutines
- When sleep mode is used by an android with a vanometric reactor installed,
  building acts as a 500w power emitter

## Other?

- Android stands aren't pawn-bound and androids with low memory will path to and use the nearest

## Cleanup

- Gene label `super neutro synthesis` vs part label `neutrosynthesizer`: one
  part, two names. Mirrors VREA's subroutine naming; confirm or unify **(decision)**
- Check patches for upstream issues and file reports/PRs
  (`Corpse_GetInspectString_TrimTrailingNewline`, `Graphic_PawnBodySilhouette`
  `_DetailScrollSpeed` case bug)
- Neutro infusion operation upstream feature PR?

## Localization

- Run the initial Steam Workshop description translation pass: create
  `.steamworkshop/Description/<Language>.txt` per target language (roster
  in CONTRIBUTING.md), localize each title per `.steamworkshop/README.md`'s
  convention (that language's vanilla "archotech" term plus its android
  vocabulary, no English brand appended), and sync each language's
  `AAH_SettingsCategory` Keyed value to its title line. The
  `.steamworkshop/` structure and process landed 2026-08-18 with only
  `English.txt` so far, and in-game Keyed/DefInjected strings are also
  still English-only (see the Localization section in CLAUDE.md).

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
