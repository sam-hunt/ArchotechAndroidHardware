# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Archotech Android Hardware** is a RimWorld 1.6 mod that adds archotech-tier android body parts for Vanilla Races Expanded - Android (VREA). Requires Harmony, Biotech DLC, and VREA.

**Current content:**
- Vanometric reactor — a permanent, unlimited-power reactor replacement for androids.
- Psychic transceiver: an archotech brain implant that grants psychic sensitivity to androids (+20% PsychicSensitivity).

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
├── Core/           # ModInitializer (Harmony setup)
├── Hediffs/        # Hediff classes (gene lifecycle management)
├── Patches/        # Harmony patches (VREA compatibility fixes)
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
ThingDefs suppress the parent's `recipeMaker` via `<recipeMaker Inherit="False" />` and define explicit crafting RecipeDefs with `<allowMixingIngredients>true</allowMixingIngredients>`. This is required because the default NoMix ingredient search groups things by `thing.def` and checks `ThingFilter.Allows(ThingDef)`, which does NOT unwrap MinifiedThings. The AllowMix path uses `ThingFilter.Allows(Thing)` which calls `GetInnerIfMinified()` and correctly matches minified buildings (VanometricPowerCell, PsychicEmanator).

**Namespace Convention:** Use `*Patches` suffix for patch namespaces to avoid RimWorld type conflicts (e.g., `VREAPatches`).

### Harmony Patches (reflection-based, no VREA DLL dependency)

All patches target VREA classes via `AccessTools.TypeByName()` — pure reflection, no compile-time coupling to VREAndroids.dll.

1. **`AlertAndroidsLowOnPower_Culprits`** — Prefix replacing VREA's `Alert_AndroidsLowOnPower.get_Culprits` with a null-safe version. VREA's original calls `.CurLevelPercentage` on a null need (our hediff disables it), causing a periodic NRE.

2. **`PawnHealthTracker_ShouldBeDowned`** — Postfix on `Pawn_HealthTracker.ShouldBeDowned`. VREA's Prefix forces androids downed when no `Hediff_AndroidReactor` type is found (checked via `OfType<>`). Our hediff uses `Hediff_AddedPart`, so VREA thinks the android is reactor-less. The Postfix restores the normal capacity-based check for pawns with our hediff.

3. **`RecipeInstallAndroidPart_ApplyOnPawn`** — Prefix on VREA's `Recipe_InstallAndroidPart.ApplyOnPawn`. VREA calls `RestorePart()` which destroys hediffs without spawning `spawnThingOnRemoved` items. This Prefix ejects `AAH_`-prefixed body parts and removes their companion genes before the original method runs. The Postfix cleans up any orphaned genes as a safety net. Handles all `AAH_` hediffs generically via the defName = geneName convention.

4. **`GeneUIUtility_RecacheGenes`** — Postfix on VREA's `RecacheGenes`. Injects all `AAH_`-prefixed companion genes into the xenogenes display list so they appear in VREA's custom gene inspector UI.

## Debugging

1. **Enable RimWorld Dev Mode:** Settings > Dev Mode > Logging
2. **Log locations:**
   - **Windows:** `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
   - **WSL:** `/mnt/c/Users/*/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Player.log`
3. **Logging:** Use `Log.Message("[Archotech Android Hardware] ...")` for mod-specific logs
4. **Inspect VREA defs:** VREA mod is at `$RIMWORLD_PATH/../../workshop/content/294100/2975771801/1.6/Defs/`
5. **Inspect RimWorld API:** `ilspycmd "/mnt/c/.../RimWorldWin64_Data/Managed/Assembly-CSharp.dll" -t "Namespace.ClassName"`
