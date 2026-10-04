# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Archotech Android Hardware** (AAH) is a RimWorld 1.6 mod adding archotech-tier body parts for Vanilla Races Expanded - Android (VREA). Requires Harmony, Biotech and VREA; Vanilla Expanded Framework (VEF) is a real transitive dependency (via VREA) that we use directly. Player-facing feature copy lives in `README.md` / `About/About.xml`; this file covers mechanics and footguns only.

Content, by mechanism:

- **Reactors** (each replaces VREA's reactor in the reactor slot; energy is stored on the item, so it transfers between androids):
  - _Vanometric_ — `disablesNeeds` removes the power need outright.
  - _Thanatic_ — companion gene biostatMet −4 feeds VREA's `PowerEfficiencyToPowerDrainFactorCurve` (the drain rate is whatever that curve yields, not a constant we ship); refilled by humanlike kills; overflow → `AAH_ThanaticOvercharge` buff (go-juice envelope); kills dessicate the corpse; at 0 energy the host **dies** and the reactor ejects partially recharged. A corpse-side extraction job recovers it (`Source/1.6/Jobs/`).
  - _Grav_ — biostatMet +4 (mirror of Thanatic); refilled by the `gravRefillAmount` fraction on gravship **landing** (Odyssey); overflow → `AAH_GravOvercharge`; at 0 energy the host is **downed**, not killed. Crafting is Odyssey-gated; the part still works without it.
- **Implants:** _Psychic transceiver_ (suppresses VREA's ×0 psychic-deafness factor; `transceiverSensitivityOffset` is pushed into the hediff stage's `statOffset` by `ApplyTransceiverSensitivityOffset` since stat offsets aren't read live; also reopens awakened androids to behavior-station reprogramming, setting-gated), _Archotech mnemocore_ (removes the memory need), _Neutrosynthesizer_ (kidney; actively reduces `VREA_NeutroLoss`).
- **Self-Determination inspiration** (`Source/1.6/Inspirations/`) — lets an awakened android reprogram at VREA's behavior station, which otherwise refuses awakened colonists. Works because VREA subroutine genes are not `removeWhenAwakened`; the station's `CanAcceptPawn` refusal is the only blocker. The transceiver is the permanent counterpart; both feed the same patch.
- **Magus of the Abyss scenario** (`1.6/Defs/ScenarioDefs/`, `Source/1.6/Scenarios/`) — New Utopia inverted: one awakened android with a thanatic reactor pre-installed, all factions hostile. The four `ScenPart_PawnModifier` subclasses are def-parameterised and reusable; all apply from `ModifyPawnPostGenerate` (late hook, so hediff `PostAdd` wiring fires) and are idempotent across re-rolls. The red-eyes part references `VREA_Eyes_Red`, a VREA-**implied** clone of the vanilla gene (that clone is what VREA's dialogs show; implied defs register before cross-refs resolve, so XML may name it).

## Build Commands

```bash
# Build the mod (outputs to 1.6/Assemblies/ AND atomically redeploys to the RimWorld Mods folder)
dotnet build ArchotechAndroidHardware.sln -c Release

# Stage the mod into an arbitrary folder (used by CI; same manifest as the local deploy)
dotnet build Source/1.6/ArchotechAndroidHardware.csproj -c Release \
  -t:StageMod -p:StageDir=/path/to/output/ArchotechAndroidHardware

# Run the test suite (native WSL; mono hosts the net472 runner)
dotnet test Tests/1.6/ArchotechAndroidHardware.Tests.csproj
```

The build auto-detects the RimWorld install (Windows/Linux/Mac, incl. WSL → Windows) and falls back to `Krafs.Rimworld.Ref` in CI. **WSL:** set `RIMWORLD_PATH` in `~/.bashrc` (e.g. `/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld`).

### Deployment

The `StageMod` target in `Source/1.6/ArchotechAndroidHardware.csproj` is the **single source of truth** for what ships: its `_ModFiles` ItemGroup matches the well-known RimWorld content folders at the mod root and under any version/`Common` folder, whitelisted by runtime extension (`.dll`, `.xml`, `.png/.jpg`, `.wav/.mp3/.ogg`, `.txt`; no `.pdb`/`.psd`/`.dds`), and wipes the target before recopying so renames never linger. Every Release build calls it with `StageDir = $RIMWORLD_PATH/Mods/ArchotechAndroidHardware`; CI's `release.yml` calls the same target for the zip. Only a brand-new file _type_ needs a manifest edit.

A machine-local Stop hook (`.claude/hooks/sync-mod.sh`, untracked) rebuilds + redeploys after any turn that touched mod source/content and warns on build failure. Its `find` watch list must cover every content root `StageMod` ships (root, any version folder, and the compat roots `Mods/` and `*/Mods/`), or edits under a missed root silently stop redeploying. `.gitignore` tracks only `.claude/skills/`. Release tags include `X.Y.Z-rc.N` candidates (CHANGELOG-less and Workshop-less, with the suffix only in `modVersion` and `AssemblyInformationalVersion`); `release.yml` treats any suffixed tag as a prerelease to match, so keep `/release` step 6 in step with that scheme.

## Architecture

### Directory Structure

```
About/                      # About.xml, ModIcon.png, Preview.png
1.6/
├── Defs/                   # DamageDefs, GeneCategoryDefs, GeneDefs, HediffDefs, InspirationDefs,
│                           # JobDefs, RecipeDefs, ResearchProjectDefs, RulePackDefs, ScenPartDefs,
│                           # ScenarioDefs, ThingCategoryDefs, ThingDefs, ThoughtDefs
├── Patches/                # XPath patches (VREA gene exclusion tags, stock-reactor visuals, DLC/VFE recipe tweaks)
└── Languages/English/Keyed/AAH_UI.xml   # All Keyed strings (AAH_ prefix)
Textures/                   # Items, body attachments, gene icons, designation/command icons, motes
Scripts/                    # check-translations.py, refresh-translation-expectations.py,
                            # integration-smoke-test.py, expected-injections.json (sidecar)
Tests/1.6/                  # Headless xUnit suite
Source/1.6/
├── Core/                   # Mod + settings (Settings/ partials), AAH_DefOf, AAHReactorDefs, SurgeryState
├── Hediffs/                # Hediff classes (gene lifecycle; Thanatic/Grav drain, refill, death/down)
├── Inspirations/           # Self-Determination worker + SelfDeterminationUtility
├── Jobs/                   # Thanatic corpse extraction: WorkGiver, JobDriver, FloatMenuOptionProvider
├── Motes/                  # Reactor glow mote, Thanatic drain particle + silhouette aura
├── Patches/                # Harmony patches + AAHPartEjector helper
├── Rendering/              # Reactor chassis/glow render-node workers + ReactorGlow opacity helper
├── Scenarios/              # Magus ScenParts
├── Things/                 # Reactor item classes (stored energy), Thanatic FX controllers
└── Thoughts/               # Decaying Self-Determination-overridden thought
l10n/                       # rimworld-l10n submodule (shared translation toolkit)
.steamworkshop/             # Workshop title/description per language (not shipped)
```

### Conventions

- **`AAH_` prefix** on every def. Companion-part convention: hediff defName = gene defName (= item defName for parts).
- **Def references go through `AAH_DefOf`** (`Core/AAH_DefOf.cs`), split into type-scoped classes (`AAH_HediffDefOf`, `AAH_GeneDefOf`, `AAH_ThingDefOf`, …) because `[DefOf]` binds by field name and the convention above reuses one defName across types. Optional-content defs carry `[MayRequire]`; VREA defs are plain (fail loud). Don't add `DefDatabase.GetNamed` calls.
- **`AAHReactorDefs.All`** (`Core/`) is the canonical reactor-hediff list (downing patch, scenpart picker). **A new reactor hediff must be added there.**
- **Namespaces:** `*Patches` suffix for patch namespaces (avoids RimWorld type clashes). **Comments:** plain `//` only, no XML doc comments. **Logs:** prefix `[Archotech Android Hardware]`.
- **No `?.`/`??` on `UnityEngine.Object` receivers** (Texture2D, Material, …): Unity's overloaded `==` treats destroyed objects as null and `?.` bypasses it. Verse types are plain classes and fine. Enforced by UNT0007/UNT0008; see `.editorconfig` before bulk-applying RCS1146.
- **VREA is reflection-only.** No compile-time reference to `VREAndroids.dll`; patches target it via `AccessTools.TypeByName`. XML may still name VREA classes (`VREAndroids.AndroidGeneDef`) and parents (`VREA_BodyPartAndroidBase`, `VREA_AndroidBodyPartBase`, `VREA_SurgeryInstallBodyPartAndroidBase`). Our own abstract item base is `AAH_BodyPartAndroidArchotechBase` (archotech tier); the grav reactor is ultratech and parents VREA's base directly.

### Key Patterns

**Companion gene override (load-bearing).** Each part's hediff adds its companion gene as a xenogene; the gene shares an exclusion tag with the VREA gene it suppresses (tags added by `1.6/Patches/VREA_GenePatches.xml`). VREA's genes are xenogenes too, so conflicts resolve by `GeneCategoryDef.displayPriorityInXenotype` — VREA's `VREA_Subroutine` is `9999`, so `AAH_Hardware` is **`10000`**. **Any new AAH gene must live in `AAH_Hardware`** or it silently loses. Companion genes are `VREAndroids.AndroidGeneDef` with `isCoreComponent=true` so VREA's dialogs treat them as locked hardware; the `AndroidDialog_AllowArchotechOverrides` / `BehavioristStation_PreserveArchotechGenes` patches stop the tag overlap from reading as a blocking conflict or duplicating the gene on reprogram.

**Need removal** uses `disablesNeeds` in the hediff stage (vanometric: `VREA_ReactorPower`; mnemocore: `VREA_MemorySpace`, plus the gene overriding VREA's memory genes' `enablesNeeds`). Cache-based, zero per-tick cost.

**Explicit crafting recipes, `allowMixingIngredients=true`.** ThingDefs set `<recipeMaker Inherit="False" />`. The NoMix ingredient path checks `ThingFilter.Allows(ThingDef)` and never unwraps `MinifiedThing`, so minified buildings (vanometric cell, psychic emanator, violence generator) only match via the AllowMix path. The costList that remains on each ThingDef exists only to satisfy the validator (or, for vanometric/implants, to drive market value); Thanatic and Grav carry explicit `MarketValue`.

**Optional dependency scoping.** Only _crafting_ recipes may depend on non-baseline content; hediffs, items and surgery recipes always load. Thanatic has two: `AAH_MakeThanaticReactor` (`MayRequire` Anomaly, from a shard) and `AAH_SalvageThanaticReactor` (`MayRequire` VFE Power, three reactors from a violence generator; deliberately research-ungated, see the recipe comment). Grav's recipe needs Odyssey.

**Ejection and energy transfer.** `RecipeInstallAndroidPart_ApplyOnPawn` routes every `AAH_` hediff eviction through `AAHPartEjector.Eject` → `ICustomAAHEjection` if implemented (reactors write their `Energy` onto the spawned item), else `spawnThingOnRemoved`. The `*InstallEnergyTransfer` patches carry the ingredient item's `storedEnergy` onto the fresh hediff (VREA creates it at `Energy=1`). New reactor types plug in via the interface, not by editing the patch.

**Power need wiring.** VREA's `Need_ReactorPower.CurLevel` looks the reactor up by def (`VREA_Reactor`), so our reactors would read 0; the `NeedReactorPower_CurLevel` getter/setter postfix falls through to the AAH reactor's `Energy`. VREA also force-downs any android without `VREA_Reactor`; `PawnHealthTracker_ShouldBeDowned` restores capacity-based downing for AAH reactors.

**Thanatic death path.** `TickInterval` checks `Energy == 0`, spawns the item with `storedEnergy = thanaticRefillAmount`, sets a re-entry guard, then `pawn.Kill`. The death-log label comes from `AAH_ThanaticDepletion` (DamageDef) + `AAH_Event_ThanaticDrain` (RulePackDef) + the culprit hediff `AAH_ThanaticDepletionCulprit` (a floating hediff passed to `pawn.Kill` as `exactCulprit`, never added to the pawn). Corpse dessication: `Notify_KilledPawn` fires before the victim's corpse exists, so victims are queued and polled for `.Corpse` (gives up after 300 ticks). Corpse extraction: `AAH_ExtractThanaticReactor` is a Designation + Job + WorkGiver; gizmos live on the hediff, the float-menu provider adds the designation and prioritises the job.

**Reactor visuals.** Chassis + additive core glow are body-parented render nodes (`Rendering/`), gated per source by `renderAahReactorAttachments` / `renderVreaReactorAttachment` via `ReactorGlow.AttachmentsEnabledFor` (checked in `CanDrawNow`, so toggles apply live). `reactorGlowMoteOverlay` layers an optional mote that punches through unnatural darkness; `scaleReactorGlowByPower` scales alpha per-draw in `GetMaterialPropertyBlock` (no rebake). VREA's stock reactor gets the same treatment via `VREA_BasicReactor_AddVisuals.xml` **plus** the `DynamicPawnRenderNodeSetup_Hediffs_GetDynamicNodes` patch (see below).

**Balance knobs** live in `Core/Settings/` partials, one per section; the class docblock in `ArchotechAndroidHardwareSettings.cs` carries the rationale. Drain rates are _not_ settings (they're the genes' biostatMet through VREA's curve).

### Harmony Patches

All patches run from `ArchotechAndroidHardwareHarmony`'s `[StaticConstructorOnStartup]` ctor. That placement is load-bearing twice: RimWorld 1.6 runs the `Mod` ctor off the main thread (VREA's cctor loads textures), and applying a detour JIT-compiles the target and runs its declaring type's cctor — before defs load, a target cctor that resolves defs breaks permanently (the BetterTradersGuild v1.1.0 incident). **Never move `PatchAll()` onto the `Mod` constructor path.** Unordered list; read each patch's source for the mechanism.

- `AlertAndroidsLowOnPower_Culprits` — null-safe prefix; vanometric removes the need VREA dereferences.
- `PawnHealthTracker_ShouldBeDowned` — capacity-based downing for `AAHReactorDefs.All` hosts.
- `RecipeInstallAndroidPart_ApplyOnPawn`, `ThanaticReactorInstallEnergyTransfer`, `GravReactorInstallEnergyTransfer` — three patches on one VREA method (ejection + energy carry-over); a new install-time hook must coexist.
- `Utils_IsAndroidGene` — `AAH_Hardware` genes count as android genes (inspector, power-efficiency total) **without** joining `allAndroidGenes` (which would make them selectable).
- `NeedReactorPower_CurLevel` (getter + setter classes) — charge bar fallthrough; setter only for dev-mode ±.
- `WorldComponentGravshipController_InitiateLanding` — grav refill on landing (not launch, so cancelled launches don't refill); recurses nested `IThingHolder`s. Odyssey only.
- `BehavioristStation_AllowSelfDetermination` — flips the awakening refusal for inspiration / transceiver hosts, re-checking the gates VREA evaluates after that line.
- `BehavioristStation_ConsumeSelfDetermination` — prefix on `FinishAndroidProject` (it ejects the occupant before returning) granting the inspiration or transceiver payoff thought.
- `BehavioristStation_PreserveArchotechGenes` — same target; strips `AAH_Hardware` genes from the project so `AddGene` doesn't duplicate the xenogene.
- `AndroidDialog_AllowArchotechOverrides` — hides override-only conflict groups from the create/modify dialog's accept gate and footer, then restores them. **Read the source first: the borrow/restore timing and the mnemocore `AndroidRAM` carve-out are subtle.**
- `DynamicPawnRenderNodeSetup_Hediffs_GetDynamicNodes` — re-emits render nodes for `VREA_Reactor`, whose base class reports `Visible == false`; without it the XPath-added stock-reactor visuals never render.
- `Graphic_PawnBodySilhouette_DrawWorker` — per-instance scroll vectors for the Thanatic silhouette aura (also works around Core's `_DetailScrollSpeed` case bug).
- `Corpse_GetInspectString_TrimTrailingNewline` — runs after VREA's postfix, which leaves a trailing newline that trips RimWorld's empty-line check. Stopgap for an upstream VREA bug.

## Debugging

Use the `rimworld-logs` skill (Player.log locations, `[Archotech Android Hardware]` prefix, `ilspycmd` against `Assembly-CSharp.dll` and `VREAndroids.dll`). VREA's XML lives at `$RIMWORLD_PATH/../../workshop/content/294100/2975771801/1.6/Defs/`; VEF is `2023507013`, VFE Power `2062943477`.

## Testing

`Tests/1.6/` is a headless xUnit (net472) suite for pure logic: settings coherence, overcharge-cap guards, `SurgeryState`, `ReactorGlow.AttachmentsEnabledFor`. Anything needing `DefDatabase`, a live `Pawn` or a `[DefOf]` cctor is out of scope. `dotnet test` builds Debug and `DeployToModFolder` is Release-gated, so a test run never swaps the deployed DLL. If a run fails with `BadImageFormatException`/`TypeLoadException`, a DLL is missing from the test csproj copy target (mono resolves field types eagerly). CI builds the suite but does not run it.

**Startup smoke test (pre-release):** `python3 Scripts/integration-smoke-test.py` (game closed) boots AAH with VREA, VEF and VFE Power on a pinned list and fails on any Player.log error attributed to AAH or the VREA/VEF seam. The only automated coverage the VREA patches get; wired into the release skill.

## Localization

English is the source of truth: Keyed in `1.6/Languages/English/Keyed/AAH_UI.xml`, plus a real DefInjected surface across `1.6/Defs/`. No non-English translations exist yet. Contributor rules and the roster live in `CONTRIBUTING.md`; this mod's glossary in the `translate` skill.

- **`l10n/` submodule** holds the family toolkit; `Scripts/check-translations.py` and `refresh-translation-expectations.py` are thin shims over it. Never edit `l10n/` here (upstream lives at `~/dev/rimworld-l10n`). The pin moves only at release, at the start of a translation pass, or on a new upstream major.
- **Sidecar:** `Scripts/expected-injections.json` is the authority for legal DefInjected keys; regenerate with the refresh script (boots RimWorld with Biotech, Anomaly and Odyssey active — the checker rejects a sidecar missing any of them, since MayRequire-gated defs would drop out).
- **Compat roots (not yet needed):** DefInjected ignores `MayRequire`, so the moment any gated def gets a translation it must ship from its own LoadFolders-gated `1.6/Mods/<Gate>/Languages/…` root (Odyssey: grav parts; Anomaly: `AAH_MakeThanaticReactor`; VFEPower: `AAH_SalvageThanaticReactor`). Filenames there must not collide with main-tree paths (suffix with the gate). Tracked in TODOs.md.
- **Workshop title coupling:** each language's `AAH_SettingsCategory` Keyed value must equal line 1 of `.steamworkshop/Description/<Language>.txt`.
- Translation passes run only on explicit request (token-expensive); tooling changes are always fine.

## Linting

Roslynator + Microsoft.Unity.Analyzers run on every build (warnings only, `PrivateAssets=all`); severities are pinned in `.editorconfig`. Run the `roslynator` CLI against the csproj, never the `.sln`. Formatting-only sweeps go in `.git-blame-ignore-revs`.
