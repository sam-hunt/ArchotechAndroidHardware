using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Ethereal emitter that spawns a burst of homing drain-particles over a
/// one-second window, streaming from a victim's kill location toward the
/// source android pawn.
///
/// Owns its own per-tick ticker (tickerType=Normal on the def) because the
/// hediff's TickInterval is hash-gated to the 60-tick drain cadence — too
/// coarse for a staggered particle stream within a single second. Isolating
/// the emitter as a Thing also sets up step 2's corpse-shift handling:
/// the controller can poll for <c>victim.Corpse</c> and swap the spawn
/// anchor onto it once it materialises, without the hediff having to
/// track corpse lifecycle.
/// </summary>
public class ThanaticStreamController : Thing
{
    public Pawn sourcePawn;   // homing target for every spawned particle
    public Pawn victim;        // origin — particles spawn near this pawn/corpse

    private int ticksElapsed;

    private const int StreamDurationTicks = 60;    // 1 second
    private const int TicksBetweenEmissions = 2;   // ~30 particles over the window
    // Smaller jitter keeps the stream tight; Lerp-based homing in the
    // particle itself handles direction, so no initial velocity spread
    // is needed.
    private const float SpawnPositionJitter = 0.15f;
    private const float InitialVelocityJitter = 0f;

    private static ThingDef _particleDefCache;
    private static ThingDef ParticleDef =>
        _particleDefCache ??= DefDatabase<ThingDef>.GetNamed("AAH_ThanaticDrainParticle", errorOnFail: false);

    protected override void Tick()
    {
        base.Tick();
        if (ticksElapsed >= StreamDurationTicks || sourcePawn == null || MapHeld == null)
        {
            Destroy();
            return;
        }
        if (ticksElapsed % TicksBetweenEmissions == 0)
            EmitParticle();
        ticksElapsed++;
    }

    private void EmitParticle()
    {
        if (ParticleDef == null) return;
        if (sourcePawn == null || !sourcePawn.Spawned || sourcePawn.Dead) return;

        var spawnPos = ResolveSpawnPos() + Gen.RandomHorizontalVector(SpawnPositionJitter);
        var particle = (Mote_ThanaticDrainParticle)ThingMaker.MakeThing(ParticleDef);
        particle.homingTarget = sourcePawn;
        particle.exactPosition = spawnPos;
        particle.velocity = Gen.RandomHorizontalVector(InitialVelocityJitter);
        GenSpawn.Spawn(particle, spawnPos.ToIntVec3(), MapHeld);
    }

    private Vector3 ResolveSpawnPos()
    {
        // Step 1: use the victim's last-live DrawPos while the Pawn is
        // still spawned, otherwise fall back to the Corpse. Step 2 will
        // swap primary anchor to the corpse once it exists, so the stream
        // origin matches the corpse's actual (possibly shifted) cell
        // rather than the pre-kill pawn position.
        if (victim != null && victim.Spawned) return victim.DrawPos;
        var corpse = victim?.Corpse;
        if (corpse != null && corpse.Spawned) return corpse.DrawPos;
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
