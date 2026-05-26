# Thanatic Reactor FX — RimWorld FX API Reference

Scratch notes from investigation of RimWorld's FX subsystems while building
the Thanatic Reactor kill / drain-death visuals. Not a tutorial — a
dense reference so a fresh session can pick up the work without re-
researching. Everything below was verified by decompiling
`Assembly-CSharp.dll` and inspecting shipped `Defs/`.

## 1. The three FX primitives

| | **Fleck** | **Mote** | **Effecter** |
|---|---|---|---|
| Def type | `FleckDef` | `ThingDef` (parent `MoteBase`) | `EffecterDef` |
| Underlying class | `struct FleckStatic : IFleck` | `class Mote : Thing` (or `MoteAttached`) | `class Effecter` |
| Per-instance ticking | None (interpolated from spawn params) | Yes — overridable `Tick()` | Yes — `EffectTick(A, B)` |
| Velocity | `velocity`, `acceleration` locked at spawn | Mutate `exactPosition` / `velocity` freely | N/A (orchestrator) |
| Attachment | No | `MoteAttached.link1` → a `Thing` | Via `SubEffecter`'s target |
| Shaders / anim sheets | Limited | Full (e.g. `Graphic_MoteWithAgeSecs`, `Graphic_PawnBodySilhouette`) | Via child motes / flecks |
| Cost | Cheapest | Medium | Medium — spawns motes/flecks |
| Use for | Sparks, puffs, impacts, splashes | Auras, sustained glows, silhouettes, anything that needs per-tick logic | Orchestrating patterns: bursts, continuous streams, sound+visual pairs |

**Key limitation of flecks**: `acceleration` is a constant vector baked at
spawn. There is no steering API. For homing behaviour, a custom `Mote`
subclass is required.

**Key benefit of motes**: they are real `Thing`s — they tick, serialize,
attach. Use when you need per-frame logic or attachment.

**Key benefit of effecters**: they are reusable patterns you can reference
by `EffecterDef` name and trigger from code or from a `DamageDef` /
ability / comp slot. When you just want "play the EMP effect at this
cell", you want an effecter.

## 2. Spawning from C#

### Fleck

```csharp
FleckMaker.ThrowDustPuff(position, map, scale);
FleckMaker.ThrowGeneric(position, map, fleckDef);
FleckMaker.Static(position, map, fleckDef, scale);
```

### Mote

```csharp
// Attached (follows a Thing — pawn, corpse, building):
MoteMaker.MakeAttachedOverlay(thing, moteDef, offset);

// Unattached (fixed position, optional initial velocity):
MoteMaker.MakeStaticMote(position, map, moteDef, scale);
MoteMaker.ThrowGeneric(position, map, moteDef, color);

// Manual (when you need to set subclass-specific fields before spawn):
var mote = (MyCustomMote)ThingMaker.MakeThing(moteDef);
mote.myCustomField = value;
mote.exactPosition = somePos;
(mote as MoteAttached)?.Attach(someThing);
GenSpawn.Spawn(mote, cell, map);
```

### Effecter

```csharp
// One-shot:
var effecter = effDef.Spawn();
effecter.Trigger(new TargetInfo(source), new TargetInfo(target));
effecter.Cleanup();

// Maintained (caller owns lifecycle, must call EffectTick each tick):
var effecter = effDef.SpawnMaintained(sourceTarget, map, scale);
// ... each tick: effecter.EffectTick(A, B);
// ... when done: effecter.Cleanup();
```

### Which `Spawn` overload

`EffecterDef.Spawn()` takes no args and doesn't anchor to a position —
targets come in at `Trigger` time. `Spawn(IntVec3, Map)` and
`Spawn(Thing, Map)` pre-anchor. For one-shots the difference doesn't
matter; `Trigger(A, B)` sets both endpoints regardless.

## 3. `Mote_ResurrectAbility` anatomy

Defined at `Core/Defs/Effects/Mote_Visual.xml` (~line 324). This is the
gold-standard "glowing pawn silhouette" pattern and is what we're cloning
for the Thanatic auras.

```xml
<ThingDef ParentName="MoteBase">
  <defName>Mote_ResurrectAbility</defName>
  <thingClass>MoteAttached</thingClass>
  <altitudeLayer>MoteOverhead</altitudeLayer>
  <mote>
    <fadeInTime>0.35</fadeInTime>
    <fadeOutTime>0.25</fadeOutTime>
    <solidTime>999999</solidTime>
    <needsMaintenance>True</needsMaintenance>
  </mote>
  <graphicData>
    <graphicClass>Graphic_PawnBodySilhouette</graphicClass>
    <color>(0.45, 0.85, 0.52, 1.0)</color>
    <shaderType>MoteMultiplyAddScroll</shaderType>
    <texPath>Things/Mote/Transparent</texPath>
    <shaderParameters>
      <_MultiplyTexA>/Things/Mote/Cloudy_B</_MultiplyTexA>
      <_MultiplyTexB>/Things/Mote/Cloudy_C</_MultiplyTexB>
      <_DetailTex>/Things/Mote/Speckles</_DetailTex>
      <_detailIntensity>1.3</_detailIntensity>
      <_texAScale>0.4</_texAScale>
      <_texBScale>0.3</_texBScale>
      <_DetailScale>1.25</_DetailScale>
      <_texAScrollSpeed>(0, 0.15, 0)</_texAScrollSpeed>
      <_texBScrollSpeed>(0.12, 0, 0)</_texBScrollSpeed>
      <_detailScrollSpeed>(0.15, 0.15, 0)</_detailScrollSpeed>
    </shaderParameters>
  </graphicData>
</ThingDef>
```

The visible effect is **one mote, one draw call, one shader pass**. No
effecter wraps it. In the vanilla game it's used as the `warmupMote` on
the resurrect ability; our clone triggers it as a self-ending mote.

### The four visual components of the silhouette effect

1. **Silhouette mask (shape)** — `Graphic_PawnBodySilhouette` uses the
   pawn's body and head textures as an alpha mask. Only pixels inside the
   pawn's shape render. The mask flips with pawn facing because different
   rotation sprites are used; UV orientation rotates with facing as a
   consequence.

2. **Color tint** — `color` tag is a flat multiply across the whole pass.
   Green `(0.45, 0.85, 0.52)` for resurrect; our clones use
   `(0.85, 0.12, 0.18)` crimson.

3. **Interior fill** — three textures combined by
   `MoteMultiplyAddScroll`:
   - `_MultiplyTexA: Cloudy_B` — grey cloud, tiled at `_texAScale`,
     scrolled by `_texAScrollSpeed`. Multiplied in.
   - `_MultiplyTexB: Cloudy_C` — different cloud pattern, tiled at
     `_texBScale`, scrolled by `_texBScrollSpeed`. Multiplied in.
   - `_DetailTex: Speckles` — fine dot layer, tiled at `_DetailScale`,
     scrolled by `_detailScrollSpeed`, scaled up by `_detailIntensity`.
     Added on top.
   - The three different scroll vectors produce a parallax / swirl that
     reads as "energy flow" rather than a static tint.

4. **Fade timings** — standard `fadeInTime` + `solidTime` + `fadeOutTime`.
   With `needsMaintenance=True` the mote treats `solidTime` as "hold
   until released" and relies on an external ability caller to invoke
   `mote.Maintain()` each tick. **Dropping `needsMaintenance`** makes the
   mote use finite `solidTime` as its real lifespan and self-destruct.

## 4. `Graphic_PawnBodySilhouette` internals

Full source decompiled in investigation. Key points:

- Base chain: `Graphic_PawnBodySilhouette : Graphic_Mote : Graphic_Single : Graphic`.
- **Private fields**: `GraphicRequest request`, `Pawn lastPawn`,
  `Rot4 lastFacing`, `Material bodyMaterial`, `Material headMaterial`.
- `DrawWorker(Vector3, Rot4, ThingDef, Thing, float)` is fully overridden
  (doesn't chain to base). Bundles all rendering into one method.
- Caches `bodyMaterial` / `headMaterial` per
  `(lastPawn, lastFacing)` — they get recreated via `MakeMatFrom` whenever
  pawn or rotation changes. **Materials are shared** across motes within
  a graphic instance — if multiple silhouette motes of the same def draw
  in sequence, they thrash the cache (each draw re-creates the material
  for its own pawn/facing).
- Each draw sets `ShaderPropertyIDs.PawnCenterWorld`,
  `PawnDrawSizeWorld`, `AgeSecs`, and `Color` on the current material
  via `SetVector` / `SetFloat` / `SetColor` (no `MaterialPropertyBlock`
  despite `ForcePropertyBlock => true`).
- Fallback: if `mote.link1.Target.Thing` isn't resolvable, uses
  `lastPawn` as a cached reference — mote keeps drawing at the previous
  pawn's position.
- For a `Corpse` attach-target, the `InnerPawn` is used for the sprite
  shape (supports post-death attachment; this is how resurrect works on
  corpses).

### Extension options we explored

| Approach | Verdict |
|---|---|
| Subclass `Graphic_PawnBodySilhouette` | Private fields block subclass access |
| Fork (duplicate `DrawWorker`, new class) | Compiled fine, but `graphicClass` XML resolution did not consistently dispatch to our subclass in testing — unreliable |
| Harmony `Prefix` on `DrawWorker` | Can't affect current frame — `bodyMaterial` may not exist yet |
| Harmony `Postfix` on `DrawWorker` + reflection on private fields | Works, but sets values for the NEXT render; fine for sustained motes, produces one "wrong" frame per `(pawn, facing)` change |
| Harmony `Transpiler` | Most invasive; injects into mid-DrawWorker |

Current state in this mod: **no override** — we use the vanilla graphic
as-is for the aura motes. Worth revisiting when we understand the
`graphicClass` resolution failure mode better.

## 5. `MoteMultiplyAddScroll` shader — parameter reference

Shader def: `Core/Defs/Misc/ShaderTypeDefs/ShaderTypes.xml` ->
`<shaderPath>Map/MoteMultiplyAddScroll</shaderPath>`. Actual `.shader`
source lives inside Unity asset bundles and isn't easily inspectable.

### Parameters (observed usages in shipped defs)

| Param | Type (observed) | Role |
|---|---|---|
| `_MultiplyTexA` | Texture path | First cloud / pattern layer, multiplied in |
| `_MultiplyTexB` | Texture path | Second cloud / pattern layer, multiplied in |
| `_DetailTex` | Texture path | Third layer (usually sparser, added on top) |
| `_texAScale` | float | UV tile scale of `_MultiplyTexA` |
| `_texBScale` | float | UV tile scale of `_MultiplyTexB` |
| `_DetailScale` | float | UV tile scale of `_DetailTex` |
| `_detailIntensity` | float | Multiplier on detail layer contribution |
| `_texAScrollSpeed` | Vector3 or float | UV-space per-second scroll for layer A |
| `_texBScrollSpeed` | Vector3 or float | UV-space per-second scroll for layer B |
| `_detailScrollSpeed` | Vector3 or float | UV-space per-second scroll for detail |

### Scroll speed format

**Accepts both Vector3 and scalar** — vanilla defs mix usage:

- `Mote_ResurrectAbility` uses `(0, 0.15, 0)` etc. (Vector3 — treated as
  UV-space offset per second, probably via `.xy`).
- Anomaly motes use `-0.4` (scalar — probably broadcast as uniform speed
  in a default direction, shader-internal).

Unconfirmed which components the shader actually reads from the Vector3.
Our working hypothesis is `.xy` → UV scroll, matching the convention used
for `PawnCenterWorld` elsewhere (`new Vector4(drawPos.x, drawPos.z, 0, 0)`).

### Scroll is UV-space, not world-space

Critical finding: the shader samples scroll in UV space tied to the
mesh. `Graphic_PawnBodySilhouette` draws the pawn's **per-facing** body
sprite as the mask — each facing has its own UV orientation. Setting a
fixed `_texAScrollSpeed` vector produces a visible flow direction that
rotates with pawn facing.

To achieve a true world-space flow direction, the scroll vector must be
rotated per-draw by the inverse of the pawn's facing angle. (This is
what we partially implemented and then reverted — we want to revisit in
the next iteration with cleaner mental model.)

## 6. Core texture / shader / def inventory

### Useful textures (all Core unless noted)

| Path | What it is |
|---|---|
| `Things/Mote/Transparent` | 1×1 transparent — use as base when shader supplies visuals |
| `Things/Mote/Cloudy_B`, `Things/Mote/Cloudy_C` | Cloud masks for multiply-shader stacks |
| `Things/Mote/Speckles` | Fine dot / glitter layer |
| `Things/Mote/SparkThrown` | Small spark streak |
| `Things/Mote/SparkThrownBlue` | Blue variant |
| `Things/Mote/LongSparkThrown` | Longer spark streak |
| `Things/Mote/Smoke` | Smoke puff |
| `Things/Mote/Splash` | Liquid splash |
| `Things/Mote/ToxicDamage` | Green toxic hit |
| `Things/Mote/FeedbackGoto`, `FeedbackShoot`, `FeedbackMelee`, etc. | UI-ish feedback flecks |
| `Things/Mote/RitualEffects/SpeechLines` | Ritual lines |
| `Things/Mote/MechSparkArch` (Biotech) | Animated 7-frame electricity arc sprite sheet |
| `Things/Mote/SparkSimple` (Anomaly only) | — |

### Shaders seen

- `MoteMultiplyAddScroll` (Core) — silhouette clouds.
- `MoteGlow`, `MoteGlowDistorted` — standard spark / glow.
- `GlowAnimated` — for animated sprite sheets (`_NumFrames`, `_FramesPerSec` params).
- `Transparent`, `TransparentPostLight` — generic transparent overlays.

### Relevant effecter defs (DLC attribution)

| Def | Source | Notes |
|---|---|---|
| `DisabledByEMP` / `DisabledByEMPLarge` | Core | EMP sound + grow flash + arc flecks + smoke trickle |
| `Power_Cell_Sparks` | Core | Spark spray + `BurningPowerCell_End` sound — fits "failing reactor" flavour |
| `GiantExplosion` | Core | Dramatic flash |
| `MechBandElectricityArc` | **Biotech** (in scope) | Single oriented arc via `Mote_MechControlTakingSparkArch` |
| `ObeliskSpark` | **Anomaly** (would require `MayRequire`) | EMP sound + `Mote_SparkSimple` — both Anomaly |
| `HoraxianAbilityCasting` | **Anomaly** | Delayed sprayer pattern |

## 7. Custom extensions in this mod

### `Mote_ThanaticDrainParticle` (`Source/1.6/Motes/`)

Homing mote subclass. Fields: `Pawn homingTarget`, `Vector3 velocity`.
Override `Tick()` each tick to:

1. Resolve target position (live pawn → corpse fallback → destroy).
2. Abort if arrived (within `ArrivalRadius`, 0.55 cells).
3. `velocity = Vector3.Lerp(velocity, toTarget.normalized * MaxSpeed, SteerRate)` — Lerp-based steering gives tight homing without orbital overshoot.
4. `exactPosition += velocity`.
5. `exactRotation = Atan2(velocity.x, velocity.z) * Rad2Deg` so sparks streak along motion direction.

Serialization via `ExposeData`: scribe `homingTarget` (Scribe_References)
and `velocity` (Scribe_Values).

### `ThanaticStreamController` (`Source/1.6/Things/`)

Ethereal emitter Thing. Inherits `EtherealThingBase` (category=Ethereal,
no hitpoints, drawerType=None), `tickerType=Normal`. Fields:
`Pawn sourcePawn`, `Pawn victim`, `int ticksElapsed`.

Per-tick `Tick()`:

- Destroy after `StreamDurationTicks` (60) or when source/map is gone.
- Every `TicksBetweenEmissions` (2) ticks: spawn one
  `Mote_ThanaticDrainParticle` with `homingTarget = sourcePawn` at
  `ResolveSpawnPos() + jitter`.
- `ResolveSpawnPos`: victim.DrawPos if spawned → victim.Corpse.DrawPos
  → own DrawPos fallback.

Scribe all three fields so mid-stream save/load survives.

### Thanatic aura motes

`AAH_ThanaticAura` (2.0s total) / `AAH_ThanaticAuraLong` (5.0s total) —
plain clones of `Mote_ResurrectAbility` with crimson tint and finite
timings. Currently use **vanilla** `Graphic_PawnBodySilhouette` and
`MoteAttached` — no override on scroll direction after reverting the
directional-scroll attempt.

### Spawn sites in `Hediff_ThanaticReactor`

- `Notify_KilledPawn`: `SpawnAura(AuraShortMoteDef, victim)` +
  `SpawnStreamController(pawn, victim)`; schedules
  `sourceAuraRemainingTicks = 60` (first-kill-wins).
- `TickSourceAuraDelay`: fires `SpawnAura(AuraShortMoteDef, pawn)` when
  countdown hits zero.
- `EnterDyingState`: `SpawnAura(AuraLongMoteDef, pawn)` + sets
  `dyingTicksRemaining = 300`.
- `TickDyingCountdown` / `ExecuteDeath`: runs the real Kill after 5s.
- `SpawnAura` helper uses `MoteMaker.MakeAttachedOverlay`.

## 8. Vanilla spawn-lifecycle patterns

### Corpse shift on pawn death

`Pawn.Kill` fires `Notify_KilledPawn` on all relevant hediffs **before**
the corpse spawns. The corpse is typically spawned at the pawn's last
position, but `GenPlace.TryPlaceThing` can shift it to a neighboring
cell when the pawn's cell is full (storage, other corpses, etc.).

Consequences:
- Attaching a mote to `victim` at `Notify_KilledPawn` means link1
  references the Pawn, which de-spawns imminently. `Graphic_PawnBodySilhouette`'s
  `lastPawn` fallback keeps it rendering at the last-live position —
  usually invisible distance from the actual corpse cell.
- To attach to the corpse, you must poll until `victim.Corpse != null
  && victim.Corpse.Spawned`. See existing `_pendingDessication` pattern
  in `Hediff_ThanaticReactor` for the poll loop.

### `Hediff.TickInterval(int delta)`

- `delta` is ticks since last call. Engine aggregates and calls at
  varying intervals — don't rely on `delta==1`.
- Pawn-hashed gating: `Gen.IsHashIntervalTick(pawn, interval, delta)`
  fires approximately once per `interval` ticks, spread across pawns to
  avoid same-tick stampedes.
- For sub-second timing, prefer a dedicated `Thing` with
  `tickerType=Normal`. For multi-second timing, the 60-tick hash gate is
  fine.

### `Ethereal` ThingDefs

Use `ParentName="EtherealThingBase"` (Core) for invisible logic Things:
gives `category=Ethereal`, `drawerType=None`, `useHitPoints=false`. Add
`tickerType=Normal` for per-tick `Tick()`. Spawn via
`GenSpawn.Spawn(thing, cell, map)` — ethereal Things don't conflict
with cell occupants.

## 9. Pitfalls and open questions

### Confirmed pitfalls

- **`graphicClass` resolution for subclasses** — didn't reliably dispatch
  to our custom `Graphic_PawnBodySilhouette` subclass. Cause unknown.
  Harmony patching the base class is a reliable alternative when needed.
- **Private-material access in `Graphic_PawnBodySilhouette`** — the
  `bodyMaterial` / `headMaterial` fields are private. Reflection is the
  only route from external code (subclass sees them as inherited
  private, which C# still can't access).
- **Fleck acceleration is spawn-locked** — no homing with flecks. Use
  motes for target-tracking particles.
- **Shared material cache thrashing** — silhouette materials cache per
  `(pawn, facing)`. Multiple motes of the same def in the same frame
  cause the cache to be recreated on every draw. Setting shader params
  per-draw inside `DrawWorker` is safe; setting them outside (e.g. a
  frame earlier) risks being overwritten.
- **Mote vs MoteAttached cast** — if `thingClass` doesn't resolve,
  `(MoteAttached)ThingMaker.MakeThing(def)` throws. Good failure signal
  to confirm `thingClass` is being picked up.

### Open questions (next iteration targets)

- **Is `_texAScrollSpeed` treated as Vector3 with `.xy` or something
  else?** Vanilla defs mix scalar and vector forms — unclear
  which components the shader actually reads. Empirically we saw UV-
  space scroll that rotates with pawn facing.
- **How to cleanly rotate the scroll vector into UV space for a
  world-aligned flow?** Needs the exact facing→UV mapping, which isn't
  visible without the shader source.
- **What's the speckle layer's scroll semantics?** The user noted that
  speckles and clouds seem to flow differently. Each has its own
  `_*ScrollSpeed` param — may respond differently to the same rotation.
- **Why did `graphicClass` resolution fail for our subclass?** Compiled
  DLL contains the class (verified via `ilspycmd`). Still didn't
  dispatch. Possible causes: load order, namespace resolution quirks,
  cache timing. Worth a small test-case in isolation.

### Known-good patterns to reuse

- **Effecter for simple visual/audio pattern on a cell** — just reference
  an existing `EffecterDef` and trigger from code.
- **Silhouette aura on a pawn** — `Mote_ResurrectAbility` clone with
  tint + timings. Let `MoteAttached.Attach(pawn)` handle the shape.
- **Homing particle stream** — `Mote` subclass + ethereal
  `tickerType=Normal` Thing as emitter; spawn via `ThingMaker.MakeThing`
  to set custom fields before `GenSpawn.Spawn`.
- **Delayed / multi-phase FX inside a hediff** — `int ticksRemaining`
  scribed field decremented in `TickInterval(delta)`; fire when ≤ 0.

## 10. Files in this mod

- `1.6/Defs/ThingDefs/ThingDefs_ThanaticDeathFX.xml` — aura motes, drain
  particle, stream controller.
- `Source/1.6/Motes/Mote_ThanaticDrainParticle.cs` — homing mote.
- `Source/1.6/Things/ThanaticStreamController.cs` — ethereal emitter.
- `Source/1.6/Hediffs/Hediff_ThanaticReactor.cs` — drives kill / death
  FX via `SpawnAura` + `SpawnStreamController`, with `EnterDyingState`
  / `ExecuteDeath` split for the 5s dramatisation.
