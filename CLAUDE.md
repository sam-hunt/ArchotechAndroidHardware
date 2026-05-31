# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Archotech Android Hardware** is a RimWorld 1.6 mod that adds archotech-tier android body parts for Vanilla Races Expanded - Android (VREA). Requires Harmony, Biotech DLC, and VREA.

**Current content:**

_Reactors_ (all replace VREA's reactor in the reactor body-part slot; each stores its current energy on the item so it's transferrable between androids):

- Vanometric reactor — a permanent, unlimited-power reactor replacement. Disables the power need entirely (no drain, never depletes).
- Thanatic reactor — an alternative reactor that drains faster than baseline (acceleration comes from the companion gene's biostatMet -4 feeding VREA's `PowerEfficiencyToPowerDrainFactorCurve`, so the realised rate is whatever VREA's curve evaluates to at that point — not a constant we ship) and is refilled by humanlike kills. Overflow bleeds into a combat-buff "Thanatic Overcharge" hediff (mirrors go-juice). Kills also dessicate the victim's corpse. If the reactor runs dry the android dies (not downed) and ejects the reactor partially recharged.
- Grav reactor — drains at VREA's baseline rate (companion gene biostatMet 0) and is refilled in full whenever its host participates in a gravship launch. The refill fires on _landing_, via a postfix on Odyssey's `WorldComponent_GravshipController.InitiateLanding` that scans the gravship manifest for grav reactors (see patch #7). On depletion the android is forced _downed_ (not killed, unlike Thanatic); a later launch or a reactor swap revives them. Crafting requires the Odyssey DLC (gravcore power cell ingredient); the part still functions without it via save-transfer / dev-spawn.

_Implants:_

- Psychic transceiver — an archotech brain implant that grants psychic sensitivity to androids (+20% PsychicSensitivity).
- Archotech mnemocore — an archotech brain implant that removes the android's memory need entirely. The companion gene overrides VREA's memory genes so they no longer `enablesNeeds`, and the hediff stage's `disablesNeeds` clears any residual `VREA_MemorySpace` instance. Ends memory degradation and makes RAM subroutines unnecessary.
- Neutrosynthesizer — an archotech android kidney (replaces the kidney slot). The companion gene shares an exclusion tag with `VREA_NeutroSynthesis` to suppress it, and the hediff actively reduces `VREA_NeutroLoss` severity by 0.3/day per kidney (vs VREA's 0.05/day subroutine). Two installed give 0.6/day, exceeding human blood-loss recovery (0.5/day).

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
```

The build system auto-detects the RimWorld installation path on Windows/Linux/Mac (including WSL targeting a Windows install). For CI builds without RimWorld installed, it falls back to the `Krafs.Rimworld.Ref` NuGet package.

### Deployment

The repo lives in `~/dev/ArchotechAndroidHardware`, separate from the RimWorld Mods folder. Every local build redeploys automatically and atomically — there is no separate clean step to remember.

- **Single source of truth:** the file manifest lives in **one** place — the `_ModFiles` ItemGroup in the `StageMod` target of `Source/1.6/ArchotechAndroidHardware.csproj`. It's generic over the well-known RimWorld content folders — `About`, `Assemblies`, `Defs`, `Patches`, `Textures`, `Sounds`, `Languages`, plus root `LoadFolders.xml` — each matched at the mod root **and** under any version/`Common` folder (the `$(RepoRoot)/*/<Folder>` patterns), so a new version folder (a future `1.7/`) needs no change. Source layout is mirrored verbatim into `$(StageDir)` (via per-item `MakeRelative` metadata — note an inline `MakeRelative` inside an item transform evaluates only once, not per item). Dropping in e.g. a `Sounds/` folder deploys automatically; only a brand-new _file type_ needs a new line here.
- **Lean by extension whitelist:** only the formats RimWorld loads at runtime ship — `.dll` (no `.pdb`); `.xml` (Defs/Patches/Languages/About); `.png`/`.jpg`/`.jpeg` (Textures); `.wav`/`.mp3`/`.ogg` (Sounds); `.txt` (Languages/About, e.g. `PublishedFileId.txt`). `Verse.ModContentLoader` _lists_ `.psd`/`.dds` among acceptable texture extensions, but its runtime decode path (`Texture2D.LoadImage`) only handles PNG/JPEG — a shipped `.psd`/`.dds` would fail to render and just bloat the download, so they're **excluded**. OS junk, editor backups (incl. `.kra` art sources), dev notes, and `Source/` can never leak into a release.
- **Self-cleaning:** `StageMod` wipes `$(StageDir)` and recopies from source, so renamed/deleted Defs/Patches/Textures never linger. The post-build `DeployToModFolder` target calls `StageMod` with `StageDir = $RIMWORLD_PATH/Mods/ArchotechAndroidHardware` (only when a local RimWorld install is detected).
- **CI reuses the same target:** `.github/workflows/release.yml` invokes `StageMod` with `-p:StageDir=<release dir>` rather than its own `cp` list, so the release zip can never drift from the local deploy.
- **Stop hook (`.claude/hooks/sync-mod.sh`):** runs after each conversation turn. It rebuilds+redeploys _only when mod-relevant source/content changed since the last deploy_ (skips doc/discussion turns), logs to `$TMPDIR/aah-build.log`, and prints a warning on build failure instead of silently leaving a stale DLL in the game folder. Change-detection uses a stamp at `Source/1.6/obj/.aah-deploy-stamp`. Both the hook config (`.claude/settings.local.json`) and the helper script live under `.claude/`, which is **gitignored** — so the whole post-turn sync is local-only and untracked. If it's ever promoted to a committed config, move the helper somewhere version-controlled. The script finds the repo root via `git rev-parse`, so it works regardless of where it's relocated.

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
└── Patches/        # XPath patches (VREA gene exclusion tags)
Textures/
└── Items/          # Custom body part textures
Source/1.6/
├── Core/           # Mod subclass (Harmony setup + settings window), SurgeryState, ArchotechAndroidHardwareSettings
├── Hediffs/        # Hediff classes (gene lifecycle; Thanatic reactor drain/kill/death logic)
├── Motes/          # Reactor-glow / charge-aura mote classes
├── Patches/        # Harmony patches (VREA compatibility fixes) + AAHPartEjector helper
├── Rendering/      # Reactor-glow render-node worker + shared ReactorGlow opacity helper
├── Things/         # ThingWithComps subclasses (ThanaticReactorThing — stored energy)
└── Properties/     # AssemblyInfo
```

### Def Naming Convention

All defs use the `AAH_` prefix (Archotech Android Hardware).

### Key Patterns

**VREA Parent Defs (runtime inheritance, no compile-time dependency):**

- `VREA_BodyPartAndroidBase` (ThingDef) — android category, Ultra tech level, android body part graphic
- `VREA_AndroidBodyPartBase` (HediffDef) — isBad: false, blue label color, countsAsAddedPartOrImplant
- `VREA_SurgeryInstallBodyPartAndroidBase` (RecipeDef) — Recipe_InstallAndroidPart worker, ButcherMechanoid effect, Crafting 5

**AAH ThingDef base (`AAH_BodyPartAndroidArchotechBase`):** abstract ThingDef in `ThingDefs/ThingDefs_BodyPartBases.xml` parenting `VREA_BodyPartAndroidBase`, hoisting the fields most archotech parts share — Archotech techLevel, `AAH_BodyPartsAndroidArchotech` category, the archotech icon, and `recipeMaker Inherit="False"`. New archotech parts should parent it (Thanatic does, re-overriding only its dark `graphicData`). The grav reactor is the exception — it's ultratech, so it parents `VREA_BodyPartAndroidBase` directly. Only the _item_ ThingDefs share this base; the hediffs/recipes still parent VREA's bases directly (they carry no AAH-specific shared boilerplate).

**Companion Gene Override System:**
Each body part has a companion gene managed by its hediff class. The hediff adds its gene as a **xenogene** (`pawn.genes.AddGene(def, xenogene: true)`). The gene shares an exclusion tag with the VREA gene it overrides; XPath patches in `1.6/Patches/VREA_GenePatches.xml` add those exclusion tags to the VREA genes. Convention: hediff defName = gene defName (e.g., `AAH_VanometricReactor` hediff manages `AAH_VanometricReactor` gene).

_Override priority (load-bearing):_ VREA also adds its genes as xenogenes, so Biotech's xenogene-beats-endogene shortcut doesn't apply — conflicts are resolved by `GeneCategoryDef.displayPriorityInXenotype` (higher wins via `GenesInOrder` descending sort in `Pawn_GeneTracker.CheckForOverrides`). VREA sets `VREA_Subroutine` to `9999` as a sentinel to beat every vanilla category (vanilla `Archite` is `1000`). Our `AAH_Hardware` category is set to **`10000`** to clear that threshold. **Any new AAH gene must live in `AAH_Hardware` (or another category with priority > 9999)** or it will silently lose the override to VREA.

**Need Removal via `disablesNeeds` (vanometric reactor):**
The hediff uses `<disablesNeeds><li>VREA_ReactorPower</li></disablesNeeds>` in its stage definition. Same built-in mechanism as the Circadian Half-cycler (Royalty DLC). Cache-based via `HediffSet.CacheNeeds()`, zero per-tick cost.

**Psychic Sensitivity (psychic transceiver):**
Androids are naturally psychically inert. VREA encodes this as the VREA_PsychicallyDeaf gene (PsychicSensitivity factor of zero). The companion gene (AAH_PsychicTransceiver) overrides it via the AAH_AndroidPsychic exclusion tag, removing the zero factor so the hediff's +0.20 statOffset takes effect. Uses `Recipe_InstallImplant` (not `Recipe_InstallAndroidPart`) because it's a brain implant, not a body part replacement. Removal surgery uses `VREA_SurgeryAndroid` parent (Crafting skill, no medicine, since androids are non-biological).

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

**Reactor core glow (two render paths):**
Every reactor (Vanometric / Thanatic / Grav and VREA's stock reactor) shows an additive core glow via one of two mutually-exclusive paths, chosen by the `reactorGlowMode` setting: a mote overlay (`Source/1.6/Motes/`, punches through unnatural darkness) or a body-parented render node (`Source/1.6/Rendering/`, perfectly tracked but occluded). Opacity is scaled by the android's power-need percentage (`VREA_ReactorPower.CurLevelPercentage`, routed per reactor by patch #5) when the `scaleReactorGlowByPower` setting is on; the `ReactorGlow` helper is the shared opacity source for both paths. Note the render-node path scales alpha **per-draw** in `GetMaterialPropertyBlock` — RimWorld's own per-draw tint mechanism — so there is no graphic rebake to throttle.

**Death-on-depletion (Thanatic reactor):**
Unlike Vanometric (runs indefinitely) or VREA's native reactor (forces the pawn downed at 0 energy via Severity=1 capMods), Thanatic ticks a death check in its own `TickInterval`: when Energy hits 0, it spawns the reactor item (with `storedEnergy = ThanaticRefillAmount` — lore: the pawn's death recharges the reactor once more), sets a re-entry guard flag, then calls `pawn.Kill(null, null)`. The `PawnHealthTracker_ShouldBeDowned` postfix (generalised to all AAH\_ reactor types) keeps the pawn upright while Energy > 0 so VREA's "no reactor hediff → force downed" prefix doesn't pre-empt the death transition.

**Corpse dessication on humanlike kill (Thanatic reactor):**
`Hediff.Notify_KilledPawn` fires inside `Pawn.Kill` _before_ the victim's Corpse is spawned, so the hediff cannot touch it synchronously. Thanatic enqueues the victim pawn and polls `victim.Corpse` each `TickInterval`; when the corpse is spawned the hediff pushes `CompRottable.RotProgress` past the dessicated threshold. Gives up after 300 ticks if the corpse never appears (despawn / destroy races).

**Balance settings (Thanatic reactor):**
Drain rate is not a runtime setting — it's the `AAH_ThanaticReactor` gene's biostatMet feeding VREA's `PowerEfficiencyToPowerDrainFactorCurve`. Refill amount and overcharge duration parameters live in `ArchotechAndroidHardwareSettings`; see that class's docblock for the balance rationale and the full tuning surface.

**Namespace Convention:** Use `*Patches` suffix for patch namespaces to avoid RimWorld type conflicts (e.g., `VREAPatches`).

### Harmony Patches (reflection-based, no VREA DLL dependency)

All patches target VREA classes via `AccessTools.TypeByName()` — pure reflection, no compile-time coupling to VREAndroids.dll.

1. **`AlertAndroidsLowOnPower_Culprits`** — Prefix replacing VREA's `Alert_AndroidsLowOnPower.get_Culprits` with a null-safe version. VREA's original calls `.CurLevelPercentage` on a null need (Vanometric reactor disables it), causing a periodic NRE.

2. **`PawnHealthTracker_ShouldBeDowned`** — Postfix on `Pawn_HealthTracker.ShouldBeDowned`. VREA's Prefix forces androids downed when no `Hediff_AndroidReactor` type is found (checked via `OfType<>`). Our reactor hediffs use `Hediff_AddedPart` (reflection-only stance, no VREA compile dep), so VREA thinks the android is reactor-less. The Postfix restores capacity-based downing for pawns with any AAH\_ reactor (Vanometric, Thanatic, _or_ Grav). **When adding a new reactor-type hediff, extend `ReactorHediffDefNames` in this patch.**

3. **`RecipeInstallAndroidPart_ApplyOnPawn`** — Prefix on VREA's `Recipe_InstallAndroidPart.ApplyOnPawn`. VREA calls `RestorePart()` which destroys hediffs without spawning `spawnThingOnRemoved` items. This Prefix delegates ejection to `AAHPartEjector.Eject` (which dispatches to `ICustomAAHEjection` for state-preserving hediffs like Thanatic, or falls through to `spawnThingOnRemoved` for the rest) and removes companion genes before the original method runs. The Postfix cleans up any orphaned genes as a safety net. Handles all `AAH_` hediffs generically via the defName = geneName convention.

4. **`Utils_IsAndroidGene`** — Postfix on VREA's `Utils.IsAndroidGene(GeneDef)`. VREA gates three display behaviours on this check: the `GeneDef.GetDescriptionFull` / `SpecialDisplayStats` postfixes that relabel biostatMet "Metabolism" → "Power efficiency", and `GeneUIUtility.RecacheGenes` which decides which genes appear in the android inspector's hardware/subroutine sections and whose biostatMet rolls into the displayed Power efficiency total. The original only returns true for genes in VREA's private `allAndroidGenes` set, which is populated from `displayCategory = VREA_Hardware|VREA_Subroutine`. Our postfix flips the result to true for any gene in the `AAH_Hardware` category, enabling all three behaviours for AAH hardware genes without touching `allAndroidGenes` itself (that collection also feeds `AndroidGenesGenesInOrder`, which drives the android creation / behaviorist modify dialogs — we deliberately don't want our companion genes selectable there).

5. **`NeedReactorPower_CurLevel`** — Postfix on VREA's `Need_ReactorPower.CurLevel` getter _and_ setter. VREA's accessors both resolve the backing hediff by def (`VREA_Reactor`); when Thanatic replaces it, the getter returns 0 (bar reads empty) and the setter silently no-ops (dev-mode +/- buttons stop working). The getter postfix falls through to `AAH_ThanaticReactor.Energy` when VREA_Reactor is absent; the setter postfix writes the incoming value back into the hediff's `Energy` and syncs `Need.curLevelInt` (matches VREA's native setter). VREA_Reactor present takes precedence in both directions. The setter patch exists purely for dev-mode +/- round-tripping — normal gameplay writes Energy directly from drain / kill refills, never via the Need.

6. **`ThanaticReactorInstallEnergyTransfer`** — Prefix+Postfix on VREA's `Recipe_InstallAndroidPart.ApplyOnPawn`. Separate from patch #3 so ejection/gene logic and energy-transfer logic stay independent. Prefix stashes the ingredient `ThanaticReactorThing.storedEnergy`; Postfix writes it onto the freshly-added `Hediff_ThanaticReactor.Energy` (overriding the `PostAdd` default of 1f). Coexists with patch #3 on the same target method.

7. **`WorldComponentGravshipController_InitiateLanding`** — Postfix on Odyssey's `WorldComponent_GravshipController.InitiateLanding` (fires once per landing, after travel completes, on clean _or_ crash landings). Walks the gravship manifest (`Gravship.Things`, recursing nested `IThingHolder`s for reactors in inventories / carry trackers / storage) plus the on-board pawns' grav reactor hediffs, and refills each grav reactor to full. Landing-time (not launch-time) so cancel-at-destination-picker and interrupted-ritual cases correctly _don't_ refill. Odyssey types live in the main assembly so direct references are safe; the patch simply never fires without Odyssey since no gravship launches occur.

8. **`GravReactorInstallEnergyTransfer`** — Prefix+Postfix on VREA's `Recipe_InstallAndroidPart.ApplyOnPawn`, mirroring patch #6 exactly for the grav reactor: stashes the ingredient `GravReactorThing.storedEnergy` and writes it onto the freshly-added `Hediff_GravReactor.Energy`. Kept as a separate type (not a shared generic patch) so each reactor's transfer pipeline stays independently traceable. Coexists with patches #3 and #6 on the same target method.

## Debugging

1. **Enable RimWorld Dev Mode:** Settings > Dev Mode > Logging
2. **Log locations:**
   - **Windows:** `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
   - **WSL:** `/mnt/c/Users/*/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Player.log`
3. **Logging:** Use `Log.Message("[Archotech Android Hardware] ...")` for mod-specific logs
4. **Inspect VREA defs:** VREA mod is at `$RIMWORLD_PATH/../../workshop/content/294100/2975771801/1.6/Defs/`
5. **Inspect RimWorld API:** `ilspycmd "/mnt/c/.../RimWorldWin64_Data/Managed/Assembly-CSharp.dll" -t "Namespace.ClassName"`
