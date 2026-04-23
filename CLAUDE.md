# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Archotech Android Hardware** is a RimWorld 1.6 mod that adds archotech-tier android body parts for Vanilla Races Expanded - Android (VREA). Requires Harmony, Biotech DLC, and VREA.

**Current content:**
- Vanometric reactor — a permanent, unlimited-power reactor replacement for androids.
- Psychic transceiver — an archotech brain implant that grants psychic sensitivity to androids (+20% PsychicSensitivity).
- Thanatic reactor — an alternative reactor that drains faster than baseline (acceleration comes from the companion gene's biostatMet -4 feeding VREA's `PowerEfficiencyToPowerDrainFactorCurve`, so the realised rate is whatever VREA's curve evaluates to at that point — not a constant we ship) and is refilled by humanlike kills. Overflow bleeds into a combat-buff "Thanatic Overcharge" hediff (mirrors go-juice). Kills also dessicate the victim's corpse. If the reactor runs dry the android dies (not downed) and ejects the reactor partially recharged. The reactor item stores its current energy so it's transferrable between androids.

**Key Technologies:** C# (.NET Framework 4.7.2), Harmony library (reflection-only, no VREA compile-time dependency), RimWorld modding API, XML definitions

## Build Commands

```bash
# Build the mod (outputs to 1.6/Assemblies/ and deploys to RimWorld Mods folder)
dotnet build ArchotechAndroidHardware.sln -c Release

# Build only the main project
dotnet build Source/1.6/ArchotechAndroidHardware.csproj

# Clean build + deploy (removes stale files, rebuilds, redeploys)
./Scripts/clean-build.sh

# Clean deployed mod folder (use when Defs/Patches are renamed or deleted)
dotnet build Source/1.6/ArchotechAndroidHardware.csproj -t:CleanModFolder
```

The build system auto-detects the RimWorld installation path on Windows/Linux/Mac (including WSL targeting a Windows install). For CI builds without RimWorld installed, it falls back to the `Krafs.Rimworld.Ref` NuGet package.

### Deployment

The repo lives in `~/dev/ArchotechAndroidHardware`, separate from the RimWorld Mods folder. A post-build MSBuild target (`DeployToModFolder`) automatically copies runtime files to `$RIMWORLD_PATH/Mods/ArchotechAndroidHardware/`. The `Scripts/clean-build.sh` script performs a full clean build + deploy cycle and is also run automatically via a Claude Code Stop hook after each conversation turn.

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
├── Patches/        # Harmony patches (VREA compatibility fixes) + AAHPartEjector helper
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

**Companion Gene Override System:**
Each body part has a companion gene managed by its hediff class. The hediff adds its gene as a **xenogene** (`pawn.genes.AddGene(def, xenogene: true)`). The gene shares an exclusion tag with the VREA gene it overrides; XPath patches in `1.6/Patches/VREA_GenePatches.xml` add those exclusion tags to the VREA genes. Convention: hediff defName = gene defName (e.g., `AAH_VanometricReactor` hediff manages `AAH_VanometricReactor` gene).

*Override priority (load-bearing):* VREA also adds its genes as xenogenes, so Biotech's xenogene-beats-endogene shortcut doesn't apply — conflicts are resolved by `GeneCategoryDef.displayPriorityInXenotype` (higher wins via `GenesInOrder` descending sort in `Pawn_GeneTracker.CheckForOverrides`). VREA sets `VREA_Subroutine` to `9999` as a sentinel to beat every vanilla category (vanilla `Archite` is `1000`). Our `AAH_Hardware` category is set to **`10000`** to clear that threshold. **Any new AAH gene must live in `AAH_Hardware` (or another category with priority > 9999)** or it will silently lose the override to VREA.

**Need Removal via `disablesNeeds` (vanometric reactor):**
The hediff uses `<disablesNeeds><li>VREA_ReactorPower</li></disablesNeeds>` in its stage definition. Same built-in mechanism as the Circadian Half-cycler (Royalty DLC). Cache-based via `HediffSet.CacheNeeds()`, zero per-tick cost.

**Psychic Sensitivity (psychic transceiver):**
Androids are naturally psychically inert. VREA encodes this as the VREA_PsychicallyDeaf gene (PsychicSensitivity factor of zero). The companion gene (AAH_PsychicTransceiver) overrides it via the AAH_AndroidPsychic exclusion tag, removing the zero factor so the hediff's +0.20 statOffset takes effect. Uses `Recipe_InstallImplant` (not `Recipe_InstallAndroidPart`) because it's a brain implant, not a body part replacement. Removal surgery uses `VREA_SurgeryAndroid` parent (Crafting skill, no medicine, since androids are non-biological).

**Explicit Crafting Recipe (not auto-generated):**
ThingDefs suppress the parent's `recipeMaker` via `<recipeMaker Inherit="False" />` and define explicit crafting RecipeDefs with `<allowMixingIngredients>true</allowMixingIngredients>`. This is required because the default NoMix ingredient search groups things by `thing.def` and checks `ThingFilter.Allows(ThingDef)`, which does NOT unwrap MinifiedThings. The AllowMix path uses `ThingFilter.Allows(Thing)` which calls `GetInnerIfMinified()` and correctly matches minified buildings (VanometricPowerCell, PsychicEmanator, VPE_ArchotechViolenceGenerator).

**Optional dependency scoping (Thanatic reactor ↔ Vanilla Expanded Power):**
Only the *crafting* recipe of a part may depend on a non-baseline mod — the hediffs, ThingDef, and surgery recipes must always load. The Thanatic reactor's crafting RecipeDef uses `<RecipeDef MayRequire="VanillaExpanded.VFEPower">` so the recipe is skipped when VPE is absent, but the reactor still functions from save-transfer / dev-spawn / another mod's dispensing.

**Polymorphic hediff ejection (AAHPartEjector + `ICustomAAHEjection`):**
`RecipeInstallAndroidPart_ApplyOnPawn_Patch` delegates all AAH_ hediff ejection through `AAHPartEjector.Eject(Hediff, Pawn)`. Hediffs implementing `ICustomAAHEjection` handle their own spawn (used by Thanatic reactor to transfer current Energy onto the ejected item); others fall through to the default `HediffDef.spawnThingOnRemoved` path. The patch is deliberately type-agnostic — new reactor-type parts plug in via the interface, not by editing the patch.

**Reactor stateful transfer (Thanatic reactor):**
`ThanaticReactorThing : ThingWithComps` stores a `storedEnergy` float. On install, the `ThanaticReactorInstallEnergyTransfer` patch reads the ingredient Thing's `storedEnergy` in its Prefix, lets VREA's `Recipe_InstallAndroidPart.ApplyOnPawn` run (which creates the hediff at `Energy=1f`), then overwrites the hediff's Energy from the stash in its Postfix. On eject (via `ICustomAAHEjection.EjectCustom`), the current hediff Energy is written back onto a fresh Thing.

**Power need wiring without hediff-type inheritance (Thanatic reactor):**
VREA's `Need_ReactorPower.CurLevel` looks up the reactor hediff by def (`VREA_Reactor`), not by type. Since Thanatic replaces VREA_Reactor rather than inheriting from `Hediff_AndroidReactor` (kept reflection-only), the need bar would otherwise read 0 permanently. The `NeedReactorPower_CurLevel` postfix falls through to `AAH_ThanaticReactor.Energy` when VREA_Reactor is absent. The `VREA_Power` gene's `enablesNeeds` keeps the need itself in the pawn's needs list, so only the backing lookup needed patching.

**Death-on-depletion (Thanatic reactor):**
Unlike Vanometric (runs indefinitely) or VREA's native reactor (forces the pawn downed at 0 energy via Severity=1 capMods), Thanatic ticks a death check in its own `TickInterval`: when Energy hits 0, it spawns the reactor item (with `storedEnergy = ThanaticRefillAmount` — lore: the pawn's death recharges the reactor once more), sets a re-entry guard flag, then calls `pawn.Kill(null, null)`. The `PawnHealthTracker_ShouldBeDowned` postfix (generalised to all AAH_ reactor types) keeps the pawn upright while Energy > 0 so VREA's "no reactor hediff → force downed" prefix doesn't pre-empt the death transition.

**Corpse dessication on humanlike kill (Thanatic reactor):**
`Hediff.Notify_KilledPawn` fires inside `Pawn.Kill` *before* the victim's Corpse is spawned, so the hediff cannot touch it synchronously. Thanatic enqueues the victim pawn and polls `victim.Corpse` each `TickInterval`; when the corpse is spawned the hediff pushes `CompRottable.RotProgress` past the dessicated threshold. Gives up after 300 ticks if the corpse never appears (despawn / destroy races).

**Balance settings (Thanatic reactor):**
Drain rate is not a runtime setting — it's the `AAH_ThanaticReactor` gene's biostatMet feeding VREA's `PowerEfficiencyToPowerDrainFactorCurve`. Refill amount and overcharge duration parameters live in `ArchotechAndroidHardwareSettings`; see that class's docblock for the balance rationale and the full tuning surface.

**Namespace Convention:** Use `*Patches` suffix for patch namespaces to avoid RimWorld type conflicts (e.g., `VREAPatches`).

### Harmony Patches (reflection-based, no VREA DLL dependency)

All patches target VREA classes via `AccessTools.TypeByName()` — pure reflection, no compile-time coupling to VREAndroids.dll.

1. **`AlertAndroidsLowOnPower_Culprits`** — Prefix replacing VREA's `Alert_AndroidsLowOnPower.get_Culprits` with a null-safe version. VREA's original calls `.CurLevelPercentage` on a null need (Vanometric reactor disables it), causing a periodic NRE.

2. **`PawnHealthTracker_ShouldBeDowned`** — Postfix on `Pawn_HealthTracker.ShouldBeDowned`. VREA's Prefix forces androids downed when no `Hediff_AndroidReactor` type is found (checked via `OfType<>`). Our reactor hediffs use `Hediff_AddedPart` (reflection-only stance, no VREA compile dep), so VREA thinks the android is reactor-less. The Postfix restores capacity-based downing for pawns with any AAH_ reactor (Vanometric *or* Thanatic). **When adding a new reactor-type hediff, extend `ReactorHediffDefNames` in this patch.**

3. **`RecipeInstallAndroidPart_ApplyOnPawn`** — Prefix on VREA's `Recipe_InstallAndroidPart.ApplyOnPawn`. VREA calls `RestorePart()` which destroys hediffs without spawning `spawnThingOnRemoved` items. This Prefix delegates ejection to `AAHPartEjector.Eject` (which dispatches to `ICustomAAHEjection` for state-preserving hediffs like Thanatic, or falls through to `spawnThingOnRemoved` for the rest) and removes companion genes before the original method runs. The Postfix cleans up any orphaned genes as a safety net. Handles all `AAH_` hediffs generically via the defName = geneName convention.

4. **`Utils_IsAndroidGene`** — Postfix on VREA's `Utils.IsAndroidGene(GeneDef)`. VREA gates three display behaviours on this check: the `GeneDef.GetDescriptionFull` / `SpecialDisplayStats` postfixes that relabel biostatMet "Metabolism" → "Power efficiency", and `GeneUIUtility.RecacheGenes` which decides which genes appear in the android inspector's hardware/subroutine sections and whose biostatMet rolls into the displayed Power efficiency total. The original only returns true for genes in VREA's private `allAndroidGenes` set, which is populated from `displayCategory = VREA_Hardware|VREA_Subroutine`. Our postfix flips the result to true for any gene in the `AAH_Hardware` category, enabling all three behaviours for AAH hardware genes without touching `allAndroidGenes` itself (that collection also feeds `AndroidGenesGenesInOrder`, which drives the android creation / behaviorist modify dialogs — we deliberately don't want our companion genes selectable there).

5. **`NeedReactorPower_CurLevel`** — Postfix on VREA's `Need_ReactorPower.CurLevel` getter *and* setter. VREA's accessors both resolve the backing hediff by def (`VREA_Reactor`); when Thanatic replaces it, the getter returns 0 (bar reads empty) and the setter silently no-ops (dev-mode +/- buttons stop working). The getter postfix falls through to `AAH_ThanaticReactor.Energy` when VREA_Reactor is absent; the setter postfix writes the incoming value back into the hediff's `Energy` and syncs `Need.curLevelInt` (matches VREA's native setter). VREA_Reactor present takes precedence in both directions. The setter patch exists purely for dev-mode +/- round-tripping — normal gameplay writes Energy directly from drain / kill refills, never via the Need.

6. **`ThanaticReactorInstallEnergyTransfer`** — Prefix+Postfix on VREA's `Recipe_InstallAndroidPart.ApplyOnPawn`. Separate from patch #3 so ejection/gene logic and energy-transfer logic stay independent. Prefix stashes the ingredient `ThanaticReactorThing.storedEnergy`; Postfix writes it onto the freshly-added `Hediff_ThanaticReactor.Energy` (overriding the `PostAdd` default of 1f). Coexists with patch #3 on the same target method.

## Debugging

1. **Enable RimWorld Dev Mode:** Settings > Dev Mode > Logging
2. **Log locations:**
   - **Windows:** `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
   - **WSL:** `/mnt/c/Users/*/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Player.log`
3. **Logging:** Use `Log.Message("[Archotech Android Hardware] ...")` for mod-specific logs
4. **Inspect VREA defs:** VREA mod is at `$RIMWORLD_PATH/../../workshop/content/294100/2975771801/1.6/Defs/`
5. **Inspect RimWorld API:** `ilspycmd "/mnt/c/.../RimWorldWin64_Data/Managed/Assembly-CSharp.dll" -t "Namespace.ClassName"`
