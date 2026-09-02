using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Ethereal emitter that spawns a burst of homing drain-particles over a
// one-second window, streaming from a victim's kill location toward the
// source android pawn.
//
// Owns its own per-tick ticker (tickerType=Normal on the def) because the
// hediff's TickInterval is hash-gated to the 60-tick drain cadence — too
// coarse for a staggered particle stream within a single second. Isolating
// the emitter as a Thing also sets up step 2's corpse-shift handling:
// the controller can poll for victim.Corpse and swap the spawn
// anchor onto it once it materialises, without the hediff having to
// track corpse lifecycle.
public class ThanaticStreamController : Thing
{
    public Pawn sourcePawn;   // homing target for every spawned particle
    public Pawn victim;        // origin — particles spawn near this pawn/corpse

    private int ticksElapsed;

    // Pre-emission lead time so the stream begins half a second after the
    // victim aura fires — gives the kill moment a beat to land before the
    // sparks start peeling off.
    private const int EmissionStartTick = 30;       // 0.5s delay
    private const int EmissionWindowTicks = 60;     // 1s of emissions after the delay
    private const int StreamDurationTicks = EmissionStartTick + EmissionWindowTicks;
    private const int TicksBetweenEmissions = 2;   // ~30 particles over the window
    private const float SpawnPositionJitter = 0.15f;

    // Emission arc: particles peel off biased toward the source but with a
    // wide lateral spread, then the homing Lerp in Mote_ThanaticDrainParticle
    // pulls them back onto course — reads as "energy flaring off the corpse
    // then converging on the reactor" rather than a straight beam.
    private const float EmissionArcRadians = 1.0472f;   // ±30° about the source direction
    // Low starting speeds paired with the homing Lerp in
    // Mote_ThanaticDrainParticle — the Lerp accelerates particles toward
    // MaxSpeed over ~10 ticks, so a low launch reads as "peels off, then
    // accelerates onto course" without needing a separate speed curve.
    private const float InitialSpeedMin = 0.02f;
    private const float InitialSpeedMax = 0.06f;

    // Per-particle size jitter around the def's drawSize. Moderate range so a
    // mixed stream of large/small sparks reads as organic rather than the
    // pixel-identical spray we had before.
    private const float ScaleMin = 0.6f;
    private const float ScaleMax = 1.2f;

    private static ThingDef ParticleDef => AAH_ThingDefOf.AAH_ThanaticDrainParticle;

    protected override void Tick()
    {
        base.Tick();
        if (ticksElapsed >= StreamDurationTicks || sourcePawn == null || MapHeld == null)
        {
            Destroy();
            return;
        }
        if (ticksElapsed >= EmissionStartTick &&
            (ticksElapsed - EmissionStartTick) % TicksBetweenEmissions == 0)
            EmitParticle();
        ticksElapsed++;
    }

    private void EmitParticle()
    {
        if (ParticleDef == null) return;
        if (sourcePawn?.Spawned != true || sourcePawn.Dead) return;

        var spawnPos = ResolveSpawnPos() + Gen.RandomHorizontalVector(SpawnPositionJitter);
        var particle = (Mote_ThanaticDrainParticle)ThingMaker.MakeThing(ParticleDef);
        particle.homingTarget = sourcePawn;
        particle.exactPosition = spawnPos;
        particle.velocity = ComputeInitialVelocity(spawnPos);
        float scale = Rand.Range(ScaleMin, ScaleMax);
        particle.linearScale = new Vector3(scale, 1f, scale);
        GenSpawn.Spawn(particle, spawnPos.ToIntVec3(), MapHeld);
    }

    // Bias initial direction toward the source, then rotate by a random angle
    // inside EmissionArcRadians. Lateral outliers get pulled in by the homing
    // Lerp within ~10 ticks, so a wide arc is safe even with a 1s stream.
    private Vector3 ComputeInitialVelocity(Vector3 spawnPos)
    {
        float speed = Rand.Range(InitialSpeedMin, InitialSpeedMax);
        if (sourcePawn == null) return Gen.RandomHorizontalVector(speed);
        var toSource = sourcePawn.DrawPos - spawnPos;
        toSource.y = 0f;
        if (toSource.sqrMagnitude < 1e-6f) return Gen.RandomHorizontalVector(speed);
        var dir = toSource.normalized;
        float angle = Rand.Range(-EmissionArcRadians * 0.5f, EmissionArcRadians * 0.5f);
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        return new Vector3(dir.x * cos - dir.z * sin, 0f, dir.x * sin + dir.z * cos) * speed;
    }

    private Vector3 ResolveSpawnPos()
    {
        // Step 1: use the victim's last-live DrawPos while the Pawn is
        // still spawned, otherwise fall back to the Corpse. Step 2 will
        // swap primary anchor to the corpse once it exists, so the stream
        // origin matches the corpse's actual (possibly shifted) cell
        // rather than the pre-kill pawn position.
        if (victim?.Spawned == true) return victim.DrawPos;
        var corpse = victim?.Corpse;
        if (corpse?.Spawned == true) return corpse.DrawPos;
        return DrawPos;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref sourcePawn, "sourcePawn");
        Scribe_References.Look(ref victim, "victim");
        Scribe_Values.Look(ref ticksElapsed, "ticksElapsed");
    }
}
