# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Archotech Android Hardware** is a RimWorld 1.6 mod that adds archotech-tier android body parts for Vanilla Races Expanded - Android (VREA). Requires Harmony, Biotech DLC, and VREA.

**Current content:**

_Reactors_ (all replace VREA's reactor in the reactor body-part slot; each stores its current energy on the item so it's transferrable between androids):

- Vanometric reactor — a permanent, unlimited-power reactor replacement. Disables the power need entirely (no drain, never depletes).
- Thanatic reactor — an alternative reactor that drains faster than baseline (acceleration comes from the companion gene's biostatMet -4 feeding VREA's `PowerEfficiencyToPowerDrainFactorCurve`, so the realised rate is whatever VREA's curve evaluates to at that point — not a constant we ship) and is refilled by humanlike kills. Overflow bleeds into a combat-buff "Thanatic Overcharge" hediff (mirrors go-juice). Kills also dessicate the victim's corpse. If the reactor runs dry the android dies (not downed) and ejects the reactor partially recharged.
- Grav reactor — drains slower than baseline (the companion gene's biostatMet +4 feeds VREA's `PowerEfficiencyToPowerDrainFactorCurve`; the mirror of Thanatic's -4) and is refilled by the `gravRefillAmount` fraction (default 0.5) whenever its host participates in a gravship launch. The refill fires on _landing_, via a postfix on Odyssey's `WorldComponent_GravshipController.InitiateLanding` that scans the gravship manifest for grav reactors (see the `WorldComponentGravshipController_InitiateLanding` patch). Refill overflow (energy already present at launch) bleeds into the "Grav Overcharge" buff — the grav-themed counterpart to Thanatic Overcharge (cold tolerance + move/work speed, no psyfocus bump), applied/extended in `Hediff_GravOvercharge.ApplyOrExtend` from `Notify_RechargedByLaunch`. On depletion the android is forced _downed_ (not killed, unlike Thanatic); a later launch or a reactor swap revives them. Crafting requires the Odyssey DLC (gravcore power cell ingredient); the part still functions without it via save-transfer / dev-spawn.

_Implants:_

- Psychic transceiver — an archotech brain implant that grants psychic sensitivity to androids (suppressing VREA's ×0 deafness factor restores the 100% base, on which a configurable offset stacks — net = 100% + the offset). That offset is the `transceiverSensitivityOffset` setting (range −1.0…2.0, slider tagged at the vanilla psychic-trait degree offsets) — pushed into the hediff stage's `statOffset` by `ApplyTransceiverSensitivityOffset` at startup / on settings write (the XML value is just the load-time default), since a hediff stat offset isn't read live like the reactor knobs. It also permanently reopens an _awakened_ android to behavior-station reprogramming — the reliable, prerequisite-gated counterpart to the Self-Determination inspiration (same `CanAcceptPawn` override, see the `BehavioristStation_AllowSelfDetermination` patch). Lore: the psychic bridge leaves the android open to outside/archotech (i.e. player) influence — the inverse of the inspiration's own-will framing. Gated by the `enableTransceiverReprogramming` setting (default on); transceiver-driven reprogramming grants the decaying `AAH_SelfDeterminationOverridden` thought (−5→−1 mood stepping down as it ages out over 30 days; the `BehavioristStation_ConsumeSelfDetermination` patch).
- Archotech mnemocore — an archotech brain implant that removes the android's memory need entirely. The companion gene overrides VREA's memory genes so they no longer `enablesNeeds`, and the hediff stage's `disablesNeeds` clears any residual `VREA_MemorySpace` instance. Ends memory degradation and makes RAM subroutines unnecessary.
- Neutrosynthesizer — an archotech android kidney (replaces the kidney slot). The companion gene shares an exclusion tag with `VREA_NeutroSynthesis` to suppress it, and the hediff actively reduces `VREA_NeutroLoss` severity by 0.3/day per kidney (vs VREA's 0.05/day subroutine). Two installed give 0.6/day, exceeding human blood-loss recovery (0.5/day).

_Inspirations:_

- Self-Determination (`AAH_SelfDetermination`) — a mood-driven inspiration that lets an _awakened_ android temporarily reprogram its subroutines at VREA's behavior station (which normally permanently "refuses reprogramming" for awakened colonist androids). Pure enabler (no stat offsets): while active, the `BehavioristStation_AllowSelfDetermination` patch overrides the station's awakening refusal; finishing the reprogramming consumes the inspiration and grants the `AAH_SelfDeterminationFulfilled` mood memory (via the `BehavioristStation_ConsumeSelfDetermination` patch). Eligibility is gated to awakened androids carrying an AAH part by default, broadenable to all awakened androids via a setting. This works cleanly because VREA subroutine genes are _not_ `removeWhenAwakened` (only the "disabled-needs" hardware genes are), so re-selecting subroutines neither un-awakens the pawn nor is filtered by the dialog's `GeneValidator` — the station's `CanAcceptPawn` refusal is the only blocker. Worker + shared reflection helper live in `Source/1.6/Inspirations/`. The psychic transceiver is the permanent/reliable counterpart to this temporary inspiration (see its bullet above); both feed the same `BehavioristStation_AllowSelfDetermination` override.

_Scenarios:_

- Magus of the Abyss (`AAH_MagusOfTheAbyss`, `1.6/Defs/ScenarioDefs/`) — a dark inversion of VREA's New Utopia: one awakened android starting with a thanatic reactor pre-installed, framed around exterminating organic life rather than building a haven. Keeps New Utopia's research/derelict outpost/resources; drops the chased-android arrivals and wood, swaps 3 stock reactors for 2 thanatic reactors, adds a charge rifle, and starts all factions hostile. The starter is also fitted with an archotech arm (right shoulder), dark-grey marine armor (worn), and a 25% chance of red eyes.

  Bespoke `ScenPart` classes live in `Source/1.6/Scenarios/`; their `ScenPartDef`s in `1.6/Defs/ScenPartDefs/` (one def per file). The four `ScenPart_PawnModifier` subclasses are generic/reusable (def-parameterised + editor-configurable via `DoEditInterface`) and all apply from `ModifyPawnPostGenerate` (`Notify_PawnGenerated`, the late hook) so changes show on the starting-pawn config screen and the hediffs' own PostAdd wiring (e.g. reactor companion genes) fires; all are idempotent across redress/re-roll:
  - `ScenPart_StartingAndroidReactor` — fits a reactor hediff into the slot held by `VREA_Reactor` (post-`Gene_SyntheticBody.PostAdd`); our `Hediff_AddedPart` auto-evicts the stock reactor via vanilla `RestorePart`. Editor picker is driven by `AAHReactorDefs.All`.
  - `ScenPart_StartingBodyPart` — installs an added-part hediff (e.g. vanilla `ArchotechArm`, Core) into a named part; `bodyPart` def + optional `bodyPartLabel` matched against `BodyPartRecord.untranslatedCustomLabel` (raw English "right shoulder", language-independent) to pick a side.
  - `ScenPart_StartingApparelWorn` — wears (not stockpiles) an apparel def, optional stuff + `overrideColor`/`color` via `CompColorable`. Runs after vanilla gear gen, so it wears on top and drops layer conflicts.
  - `ScenPart_StartingGene` — adds a gene using the inherited `chance` roll for partial odds. **Magus uses `VREA_Eyes_Red`, not vanilla `Eyes_Red`:** VREA's `GeneDefGenerator.ImpliedGeneDefs` postfix clones every convertable cosmetic gene into a `VREA_`-prefixed implied counterpart with the hardware-gene background + `allAndroidGenes` membership — that clone is what the creation/behaviorist station dialogs and the gene inspector show. Implied defs register before cross-refs resolve, so XML can reference `VREA_Eyes_Red` directly.
  - `ScenPart_AllFactionsHostile` (`Rule` category, not a pawn modifier) — `PostGameStart` drives every goodwill faction to the −100 floor.

**Key Technologies:** C# (.NET Framework 4.7.2), Harmony library (reflection-only, no VREA compile-time dependency), RimWorld modding API, XML definitions

## Build Commands

```bash
# Build the mod (outputs to 1.6/Assemblies/ AND atomically redeploys to the RimWorld Mods folder)
dotnet build ArchotechAndroidHardware.sln -c Release

# Build only the main project (also triggers the deploy)
dotnet build Source/1.6/ArchotechAndroidHardware.csproj

# Stage the mod into an arbitrary folder (used by CI; same manifest as the local deploy)
dotnet build Source/1.6/ArchotechAndroidHardware.csproj -c Release \
  -t:StageMod -p:StageDir=/path/to/output/ArchotechAndroidHardware

# Run the test suite (native WSL; mono hosts the net472 runner)
dotnet test Tests/1.6/ArchotechAndroidHardware.Tests.csproj
```

The build system auto-detects the RimWorld installation path on Windows/Linux/Mac (including WSL targeting a Windows install). For CI builds without RimWorld installed, it falls back to the `Krafs.Rimworld.Ref` NuGet package.

### Deployment

The repo lives in `~/dev/ArchotechAndroidHardware`, separate from the RimWorld Mods folder. Every local build redeploys automatically and atomically — there is no separate clean step to remember.

- **Single source of truth:** the file manifest lives in **one** place — the `_ModFiles` ItemGroup in the `StageMod` target of `Source/1.6/ArchotechAndroidHardware.csproj`. It's generic over the well-known RimWorld content folders — `About`, `Assemblies`, `Defs`, `Patches`, `Textures`, `Sounds`, `Languages`, plus root `LoadFolders.xml` — each matched at the mod root **and** under any version/`Common` folder (the `$(RepoRoot)/*/<Folder>` patterns), so a new version folder (a future `1.7/`) needs no change. Source layout is mirrored verbatim into `$(StageDir)` (via per-item `MakeRelative` metadata — note an inline `MakeRelative` inside an item transform evaluates only once, not per item). Dropping in e.g. a `Sounds/` folder deploys automatically; only a brand-new _file type_ needs a new line here.
- **Lean by extension whitelist:** only the formats RimWorld loads at runtime ship — `.dll` (no `.pdb`); `.xml` (Defs/Patches/Languages/About); `.png`/`.jpg`/`.jpeg` (Textures); `.wav`/`.mp3`/`.ogg` (Sounds); `.txt` (Languages/About, e.g. `PublishedFileId.txt`). `Verse.ModContentLoader` _lists_ `.psd`/`.dds` among acceptable texture extensions, but its runtime decode path (`Texture2D.LoadImage`) only handles PNG/JPEG — a shipped `.psd`/`.dds` would fail to render and just bloat the download, so they're **excluded**. OS junk, editor backups (incl. `.kra` art sources), dev notes, and `Source/` can never leak into a release.
- **Self-cleaning:** `StageMod` wipes `$(StageDir)` and recopies from source, so renamed/deleted Defs/Patches/Textures never linger. The post-build `DeployToModFolder` target calls `StageMod` with `StageDir = $RIMWORLD_PATH/Mods/ArchotechAndroidHardware` (only when a local RimWorld install is detected).
- **CI reuses the same target:** `.github/workflows/release.yml` invokes `StageMod` with `-p:StageDir=<release dir>` rather than its own `cp` list, so the release zip can never drift from the local deploy.
- **Stop hook (`.claude/hooks/sync-mod.sh`):** runs after each conversation turn. It rebuilds+redeploys _only when mod-relevant source/content changed since the last deploy_ (skips doc/discussion turns), logs to `$TMPDIR/aah-build.log`, and prints a warning on build failure instead of silently leaving a stale DLL in the game folder. Change-detection uses a stamp at `Source/1.6/obj/.aah-deploy-stamp`. Both the hook config (`.claude/settings.local.json`) and the helper script stay machine-local: `.gitignore` tracks only `.claude/skills/` (shared: `release`, `translate`, `rimworld-logs`); everything else under `.claude/` is untracked. If the hook is ever promoted to a committed config, move the helper somewhere version-controlled. The script finds the repo root via `git rev-parse`, so it works regardless of where it's relocated.

**WSL Setup:** Requires `RIMWORLD_PATH` env var in `~/.bashrc` pointing to the Windows RimWorld install (e.g., `/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld`).

## Architecture

### Directory Structure

```
About/              # Mod metadata (About.xml, ModIcon.png)
1.6/
├── Assemblies/     # Compiled DLL (build output)
├── Defs/
│   ├── ThingDefs/         # Body part items (vanometric reactor, psychic transceiver)
│   ├── HediffDefs/        # Installed hediffs (power suppression, psychic sensitivity)
│   ├── GeneDefs/          # Companion genes
│   ├── GeneCategoryDefs/  # AAH_Hardware category (priority 10000, see below)
│   └── RecipeDefs/        # Crafting recipes + surgery installation recipes
├── Patches/        # XPath patches (VREA gene exclusion tags)
└── Languages/English/Keyed/AAH_UI.xml   # All player-facing settings strings (AAH_ prefix)
Textures/
└── Items/          # Custom body part textures
Scripts/
├── check-translations.py                # Deterministic localization validator (CI release gate)
├── refresh-translation-expectations.py  # Regenerates the sidecar via ../L10nProbe game boot
└── expected-injections.json             # Checked-in DefInjected expectations sidecar
Tests/1.6/          # Headless xUnit suite (settings, ReactorGlow guards, SurgeryState)
Source/1.6/
├── Core/           # Mod subclass (Harmony setup + settings window), SurgeryState, ArchotechAndroidHardwareSettings, AAH_DefOf
├── Hediffs/        # Hediff classes (gene lifecycle; Thanatic reactor drain/kill/death logic)
├── Inspirations/   # Self-Determination InspirationWorker + shared SelfDeterminationUtility
├── Motes/          # Reactor-glow / charge-aura mote classes
├── Patches/        # Harmony patches (VREA compatibility fixes) + AAHPartEjector helper
├── Rendering/      # Reactor-glow render-node worker + shared ReactorGlow opacity helper
├── Scenarios/      # Bespoke ScenParts (Magus of the Abyss: reactor/body-part/apparel/gene swaps, all-factions-hostile)
├── Things/         # ThingWithComps subclasses (ThanaticReactorThing — stored energy)
└── Properties/     # AssemblyInfo
```

### Def Naming Convention

All defs use the `AAH_` prefix (Archotech Android Hardware).

**Def references go through `AAH_DefOf`** (`Core/AAH_DefOf.cs`), mirroring vanilla's `*DefOf` / VREA's `VREA_DefOf` — bound at load (errors surface at startup, not as a runtime null) and a cheap static field read. It's **split into type-scoped classes** (`AAH_HediffDefOf`, `AAH_GeneDefOf`, `AAH_ThingDefOf`, `AAH_DesignationDefOf`, `AAH_JobDefOf`, plus the catch-all `AAH_DefOf`) because `[DefOf]` binds by field-name == defName, and the companion-part convention reuses one defName across types (e.g. `AAH_ThanaticReactor` is a Hediff, Gene _and_ Thing; `AAH_ExtractThanaticReactor` is a Designation _and_ Job). Optional-content defs (Odyssey grav parts, VFEPower violence generator) carry `[MayRequire(...)]` so they stay null instead of erroring when that content is absent; VREA defs are plain fields (hard dependency → fail loud). Don't add new `DefDatabase.GetNamed` calls — add a DefOf field. (The only legitimate runtime lookups left are the genuinely dynamic ones in `RecipeInstallAndroidPart_ApplyOnPawn`, keyed off a hediff/gene's own defName.)

### Key Patterns

**VREA Parent Defs (runtime inheritance, no compile-time dependency):**

- `VREA_BodyPartAndroidBase` (ThingDef) — android category, Ultra tech level, android body part graphic
- `VREA_AndroidBodyPartBase` (HediffDef) — isBad: false, blue label color, countsAsAddedPartOrImplant
- `VREA_SurgeryInstallBodyPartAndroidBase` (RecipeDef) — Recipe_InstallAndroidPart worker, ButcherMechanoid effect, Crafting 5

**AAH ThingDef base (`AAH_BodyPartAndroidArchotechBase`):** abstract ThingDef in `ThingDefs/ThingDefs_BodyPartAndroidArchotechBase.xml` parenting `VREA_BodyPartAndroidBase`, hoisting the fields most archotech parts share — Archotech techLevel, `AAH_BodyPartsAndroidArchotech` category, the archotech icon, and `recipeMaker Inherit="False"`. New archotech parts should parent it (Thanatic does, re-overriding only its dark `graphicData`). The grav reactor is the exception — it's ultratech, so it parents `VREA_BodyPartAndroidBase` directly. Only the _item_ ThingDefs share this base; the hediffs/recipes still parent VREA's bases directly (they carry no AAH-specific shared boilerplate).

**Companion Gene Override System:**
Each body part has a companion gene managed by its hediff class. The hediff adds its gene as a **xenogene** (`pawn.genes.AddGene(def, xenogene: true)`). The gene shares an exclusion tag with the VREA gene it overrides; XPath patches in `1.6/Patches/VREA_GenePatches.xml` add those exclusion tags to the VREA genes. Convention: hediff defName = gene defName (e.g., `AAH_VanometricReactor` hediff manages `AAH_VanometricReactor` gene).

_Override priority (load-bearing):_ VREA also adds its genes as xenogenes, so Biotech's xenogene-beats-endogene shortcut doesn't apply — conflicts are resolved by `GeneCategoryDef.displayPriorityInXenotype` (higher wins via `GenesInOrder` descending sort in `Pawn_GeneTracker.CheckForOverrides`). VREA sets `VREA_Subroutine` to `9999` as a sentinel to beat every vanilla category (vanilla `Archite` is `1000`). Our `AAH_Hardware` category is set to **`10000`** to clear that threshold. **Any new AAH gene must live in `AAH_Hardware` (or another category with priority > 9999)** or it will silently lose the override to VREA.

_Behavior-station dialog (load-bearing):_ companion genes are declared as `VREAndroids.AndroidGeneDef` with `isCoreComponent=true` (not plain `GeneDef`), so VREA's modify dialog treats them as locked hardware — non-removable by clicking (`Utils.CanBeRemovedFromAndroid` → false), like VREA's own core genes. They stay out of the dialog's selectable list / `allAndroidGenes` regardless of type — that membership is gated on `displayCategory` + `biostatArc>0`, not the class — so this is pure data, no patch. The exclusion-tag overlap that drives the override would otherwise trip VREA's strict "conflicting components" accept-gate; the `AndroidDialog_AllowArchotechOverrides` and `BehavioristStation_PreserveArchotechGenes` patches reconcile that (let the override confirm, keep the suppressed VREA gene visibly dimmed, and avoid duplicating the companion gene on reprogram).

**Need Removal via `disablesNeeds` (vanometric reactor):**
The hediff uses `<disablesNeeds><li>VREA_ReactorPower</li></disablesNeeds>` in its stage definition. Same built-in mechanism as the Circadian Half-cycler (Royalty DLC). Cache-based via `HediffSet.CacheNeeds()`, zero per-tick cost.

**Psychic Sensitivity (psychic transceiver):**
Androids are naturally psychically inert. VREA encodes this as the VREA_PsychicallyDeaf gene (PsychicSensitivity factor of zero). The companion gene (AAH_PsychicTransceiver) overrides it via the AAH_AndroidPsychic exclusion tag, removing the zero factor. Because that was a ×0 factor (not a base change), PsychicSensitivity returns to its 100% StatDef base, on which the hediff's statOffset stacks (net = 100% + the offset, not the offset alone). The offset value is the `transceiverSensitivityOffset` setting (see `ApplyTransceiverSensitivityOffset`). Uses `Recipe_InstallImplant` (not `Recipe_InstallAndroidPart`) because it's a brain implant, not a body part replacement. Removal surgery uses `VREA_SurgeryAndroid` parent (Crafting skill, no medicine, since androids are non-biological).

**Explicit Crafting Recipe (not auto-generated):**
ThingDefs suppress the parent's `recipeMaker` via `<recipeMaker Inherit="False" />` and define explicit crafting RecipeDefs with `<allowMixingIngredients>true</allowMixingIngredients>`. This is required because the default NoMix ingredient search groups things by `thing.def` and checks `ThingFilter.Allows(ThingDef)`, which does NOT unwrap MinifiedThings. The AllowMix path uses `ThingFilter.Allows(Thing)` which calls `GetInnerIfMinified()` and correctly matches minified buildings (VanometricPowerCell, PsychicEmanator, VPE_ArchotechViolenceGenerator).

**Optional dependency scoping (Thanatic reactor ↔ Vanilla Expanded Power):**
Only the _crafting_ recipe of a part may depend on a non-baseline mod — the hediffs, ThingDef, and surgery recipes must always load. The Thanatic reactor's crafting RecipeDef uses `<RecipeDef MayRequire="VanillaExpanded.VFEPower">` so the recipe is skipped when VPE is absent, but the reactor still functions from save-transfer / dev-spawn / another mod's dispensing.

**Polymorphic hediff ejection (AAHPartEjector + `ICustomAAHEjection`):**
`RecipeInstallAndroidPart_ApplyOnPawn_Patch` delegates all AAH\_ hediff ejection through `AAHPartEjector.Eject(Hediff, Pawn)`. Hediffs implementing `ICustomAAHEjection` handle their own spawn (used by Thanatic reactor to transfer current Energy onto the ejected item); others fall through to the default `HediffDef.spawnThingOnRemoved` path. The patch is deliberately type-agnostic — new reactor-type parts plug in via the interface, not by editing the patch.

**Reactor stateful transfer (Thanatic reactor):**
`ThanaticReactorThing : ThingWithComps` stores a `storedEnergy` float. On install, the `ThanaticReactorInstallEnergyTransfer` patch reads the ingredient Thing's `storedEnergy` in its Prefix, lets VREA's `Recipe_InstallAndroidPart.ApplyOnPawn` run (which creates the hediff at `Energy=1f`), then overwrites the hediff's Energy from the stash in its Postfix. On eject (via `ICustomAAHEjection.EjectCustom`), the current hediff Energy is written back onto a fresh Thing.

**Power need wiring without hediff-type inheritance (Thanatic reactor):**
VREA's `Need_ReactorPower.CurLevel` looks up the reactor hediff by def (`VREA_Reactor`), not by type. Since Thanatic replaces VREA_Reactor rather than inheriting from `Hediff_AndroidReactor` (kept reflection-only), the need bar would otherwise read 0 permanently. The `NeedReactorPower_CurLevel` postfix falls through to `AAH_ThanaticReactor.Energy` when VREA_Reactor is absent. The `VREA_Power` gene's `enablesNeeds` keeps the need itself in the pawn's needs list, so only the backing lookup needed patching.

**Reactor core glow (chassis + always-on glow render node + optional mote overlay):**
Every reactor (Vanometric / Thanatic / Grav and VREA's stock reactor) shows a chest chassis sprite plus an additive core glow. Both are body-parented render nodes (`Source/1.6/Rendering/`, perfectly tracked but occluded by unnatural darkness; also show in the colonist bar / inspect-pane portrait). The whole visual is gated per source by two master settings — `renderAahReactorAttachments` (this mod's reactors) and `renderVreaReactorAttachment` (the patched-in stock reactor) — via `ReactorGlow.AttachmentsEnabledFor`, which the chassis worker (`PawnRenderNodeWorker_ReactorAttachment`) gates in `CanDrawNow` and the glow worker (`PawnRenderNodeWorker_ReactorGlow`, a subclass) inherits, so flipping a setting hides/shows live with no tree rebuild; `ReactorGlowMote.Maintain` honours the same gate. The `reactorGlowMoteOverlay` setting (default on) additionally layers a mote overlay on top (`Source/1.6/Motes/`, punches through unnatural darkness) — `Maintain` creates/keeps the mote only while both that setting and the master gate are on, tearing it down otherwise. Opacity is scaled by the android's power-need percentage (`VREA_ReactorPower.CurLevelPercentage`, routed per reactor by the `NeedReactorPower_CurLevel` patch) when the `scaleReactorGlowByPower` setting is on; the `ReactorGlow` helper is the shared opacity source for both paths. Note the render-node path scales alpha **per-draw** in `GetMaterialPropertyBlock` — RimWorld's own per-draw tint mechanism — so there is no graphic rebake to throttle.

**Death-on-depletion (Thanatic reactor):**
Unlike Vanometric (runs indefinitely) or VREA's native reactor (forces the pawn downed at 0 energy via Severity=1 capMods), Thanatic ticks a death check in its own `TickInterval`: when Energy hits 0, it spawns the reactor item (with `storedEnergy = ThanaticRefillAmount` — lore: the pawn's death recharges the reactor once more), sets a re-entry guard flag, then calls `pawn.Kill(null, null)`. The `PawnHealthTracker_ShouldBeDowned` postfix (generalised to all AAH\_ reactor types) keeps the pawn upright while Energy > 0 so VREA's "no reactor hediff → force downed" prefix doesn't pre-empt the death transition.

**Corpse dessication on humanlike kill (Thanatic reactor):**
`Hediff.Notify_KilledPawn` fires inside `Pawn.Kill` _before_ the victim's Corpse is spawned, so the hediff cannot touch it synchronously. Thanatic enqueues the victim pawn and polls `victim.Corpse` each `TickInterval`; when the corpse is spawned the hediff pushes `CompRottable.RotProgress` past the dessicated threshold. Gives up after 300 ticks if the corpse never appears (despawn / destroy races).

**Balance settings (Thanatic reactor):**
Drain rate is not a runtime setting — it's the `AAH_ThanaticReactor` gene's biostatMet feeding VREA's `PowerEfficiencyToPowerDrainFactorCurve`. Refill amount and overcharge duration parameters live in `ArchotechAndroidHardwareSettings`; see that class's docblock for the balance rationale and the full tuning surface.

**Namespace Convention:** Use `*Patches` suffix for patch namespaces to avoid RimWorld type conflicts (e.g., `VREAPatches`).

### Harmony Patches (reflection-based, no VREA DLL dependency)

All patches target VREA (or vanilla / Odyssey) classes via `AccessTools.TypeByName()` — pure reflection, no compile-time coupling to VREAndroids.dll. The list is **unordered and referenced by name** — there's no ordering dependency between patches, so don't number them. Each entry is the high-level role plus any footgun; read the patch's own source in `Source/1.6/Patches/` for the full mechanism.

- **`AlertAndroidsLowOnPower_Culprits`** — Prefix on VREA's `Alert_AndroidsLowOnPower.get_Culprits`, null-safe: Vanometric disables the power need, so VREA's original NREs on a null need's `.CurLevelPercentage`.

- **`PawnHealthTracker_ShouldBeDowned`** — Postfix on `Pawn_HealthTracker.ShouldBeDowned` restoring capacity-based downing for pawns with an `AAH_` reactor. VREA force-downs androids it sees as reactor-less, and our reactors use `Hediff_AddedPart` (not VREA's reactor type). Reads the canonical reactor list `AAHReactorDefs.All` (`Core/`) — the single source of truth for "all reactor hediffs", also used by the starting-reactor scenpart picker. **Footgun: a new reactor-type hediff must be added to `AAHReactorDefs.All`.** (The energy-bar patch keeps a separate, narrower `IAAHReactorEnergy`-only list.)

- **`RecipeInstallAndroidPart_ApplyOnPawn`** — Prefix+Postfix on VREA's `Recipe_InstallAndroidPart.ApplyOnPawn`. Routes `AAH_` hediff ejection through `AAHPartEjector.Eject` (→ `ICustomAAHEjection`, else `spawnThingOnRemoved`) and strips/cleans companion genes; VREA's `RestorePart()` would otherwise destroy hediffs without their `spawnThingOnRemoved`. Generic over `AAH_` hediffs via the defName = geneName convention. **Three patches share this target method (the two `*InstallEnergyTransfer` below are the others) — any new install-time hook must coexist.**

- **`ThanaticReactorInstallEnergyTransfer`** / **`GravReactorInstallEnergyTransfer`** — Prefix+Postfix on the same `Recipe_InstallAndroidPart.ApplyOnPawn`, one per reactor (kept as separate types for traceability). Carry the ingredient Thing's `storedEnergy` onto the freshly-added hediff's `Energy`, overriding `PostAdd`'s default.

- **`Utils_IsAndroidGene`** — Postfix making `AAH_Hardware` genes test as android genes, so VREA's inspector shows them and folds their biostatMet into the "Power efficiency" total. **Deliberately does _not_ add them to `allAndroidGenes`** — that feeds `AndroidGenesGenesInOrder`, which would make our companion genes selectable in the creation / modify dialogs.

- **`NeedReactorPower_CurLevel`** — Postfix on VREA's `Need_ReactorPower.CurLevel` getter + setter, falling through to `AAH_ThanaticReactor.Energy` when `VREA_Reactor` is absent (else the charge bar reads 0). The setter half exists only for dev-mode +/- round-tripping.

- **`WorldComponentGravshipController_InitiateLanding`** — Postfix on Odyssey's gravship landing that refills grav reactors found in the manifest (recursing nested `IThingHolder`s) and on board pawns. Landing-time (not launch-time) so cancelled / interrupted launches don't refill. Never fires without Odyssey.

- **`BehavioristStation_AllowSelfDetermination`** — Postfix on `Building_AndroidBehavioristStation.CanAcceptPawn` flipping the awakening-only `RefusesReprogramming` refusal to accepted when the `AAH_SelfDetermination` inspiration is active **or** a psychic transceiver is installed (setting-gated); re-checks the power / quest-lodger gates VREA evaluates _after_ the awakening line.

- **`BehavioristStation_ConsumeSelfDetermination`** — Prefix on `FinishAndroidProject` granting the payoff by why the android was admitted (inspiration → consume + `AAH_SelfDeterminationFulfilled`; transceiver → decaying `AAH_SelfDeterminationOverridden`). Prefix because the method ejects the occupant before returning.

- **`BehavioristStation_PreserveArchotechGenes`** — Prefix on `FinishAndroidProject` (coexists with `BehavioristStation_ConsumeSelfDetermination` on this target) stripping `AAH_Hardware` genes from `curAndroidProject.genes`, so the still-present companion gene isn't re-added — `Pawn_GeneTracker.AddGene` doesn't dedup xenogenes, so it would duplicate. Keeps the hediff the gene's sole lifecycle authority.

- **`AndroidDialog_AllowArchotechOverrides`** — Prefix+Postfix on `Window_CreateAndroidBase.DoBottomButtons` (the create / modify dialog). The exclusion-tag overlap that drives our override otherwise reads as a blocking conflict in VREA's accept-gate + conflict footer; this hides the override-only `leftChosenGroups` from the gate + footer and restores them after — so the "suppressed" dim and the power-efficiency total stay correct — while genuine conflicts still block. **Read the source before changing: the borrow/restore frame-timing and the "genuine internal conflict" carve-out (which handles the mnemocore's shared `AndroidRAM` tag) are subtle.**

- **`Corpse_GetInspectString_TrimTrailingNewline`** — Postfix after VREA (`[HarmonyAfter("VREAndroidsMod")]` + `Priority.Last`) re-applying `TrimEndNewlines()`; VREA's own postfix leaves a trailing newline that trips RimWorld's empty-line check on android corpses (e.g. after Thanatic death adds a missing part). Stopgap for an upstream VREA bug.

## Debugging

Use the `rimworld-logs` skill — it covers Player.log locations (Windows/WSL), the `[Archotech Android Hardware]` log prefix, and API disassembly via `ilspycmd` against both vanilla's `Assembly-CSharp.dll` and VREA's `VREAndroids.dll`. VREA's XML defs live at `$RIMWORLD_PATH/../../workshop/content/294100/2975771801/1.6/Defs/`.

## Testing

`Tests/1.6/` holds an xUnit (net472) suite for the pure logic: settings field-initializer/`ResetToDefaults` coherence, overcharge-cap sentinel guards, `SurgeryState`, and `ReactorGlow.AttachmentsEnabledFor`'s headless-safe branches. Tests are headless — anything needing `DefDatabase`, a live `Pawn`, or a `[DefOf]` static constructor is out of scope (documented per-test). Run natively from WSL with `dotnet test Tests/1.6/ArchotechAndroidHardware.Tests.csproj` (vstest hosts the net472 suite via mono; if a run fails with `BadImageFormatException`/`TypeLoadException`, a DLL is missing from the test csproj copy target — see the Assembly-CSharp-firstpass comment there: mono resolves field types eagerly where the Windows CLR is lazy). `dotnet test` builds Debug by default, so `DeployToModFolder` is Release-gated — test runs never swap the deployed mod DLL for a Debug build. CI builds the Tests project but does not run it.

## Localization

English is the source of truth: Keyed strings in `1.6/Languages/English/Keyed/AAH_UI.xml` (`AAH_` prefix), plus a real DefInjected surface (labels/descriptions across the defs under `1.6/Defs/`). The pipeline is shared with the sibling mod repos:

- `python3 Scripts/check-translations.py [--strict]` — deterministic validator; CI release gate. This repo's copy carries three fixes the siblings should back-port: literal-`\n` normalization, `DEF_TYPE_ALIASES` (subclass-declared defs like VREA's `AndroidGeneDef` dump under their base type), and an Anomaly-inclusive `REQUIRED_DLCS`.
- `Scripts/expected-injections.json` — checked-in sidecar of every DefInjected key the live game expects; regenerate with `python3 Scripts/refresh-translation-expectations.py` (boots RimWorld via `../L10nProbe`; game must be closed). **`CANONICAL_ACTIVE_MODS` ids must stay lowercase** — MayRequire's active-check is case-exact even though mod loading isn't.
- The `translate` skill holds the family per-language grammar/glossary knowledge (VREA's English strings are a grounding source); `CONTRIBUTING.md` carries the public roster (English only so far). No non-English translations exist yet.
- **Workshop title coupling:** each language's `AAH_SettingsCategory` Keyed value is the localized Steam Workshop title and must equal the title line (line 1) of `.steamworkshop/Description/<Language>.txt` — always change the two together (English keeps `Archotech Android Hardware` in both).

## Linting

Roslynator.Analyzers runs on every build (warnings only, never fails the build; `PrivateAssets=all` so nothing ships). Severities are pinned in `.editorconfig`, along with the no-XML-doc-comments convention (plain `//` only). Formatting-only sweeps are registered in `.git-blame-ignore-revs`.
