using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Homing particle that streams from a victim's kill tile toward the source
// android. Unlike a stock fleck — whose velocity and acceleration are
// locked at spawn — this mote recomputes its steering each tick, so it
// tracks the android even as it moves.
//
// Self-destructs on reaching the target, losing its target with no corpse
// fallback, or the def's natural fade+solid+fade lifespan expiring (safety
// net so stragglers never linger).
//
// Spawned in batches by ThanaticStreamController.
public class Mote_ThanaticDrainParticle : Mote
{
    public Pawn homingTarget;
    public Vector3 velocity;

    // Steering model: direction is Lerped toward "straight at the target"
    // each tick at SteerRate. ArrivalRadius=0 means the only arrival
    // condition is the overshoot landing — the particle reaches the target
    // exactly, then destroys.
    private const float SteerRate = 0.20f;
    private const float MaxSpeed = 0.18f;
    private const float ArrivalRadius = 0f;

    protected override void Tick()
    {
        base.Tick();
        var targetPos = ResolveTargetPos();
        if (!targetPos.HasValue)
        {
            Destroy();
            return;
        }
        var toTarget = targetPos.Value - exactPosition;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        if (dist < ArrivalRadius)
        {
            Destroy();
            return;
        }
        var desiredVelocity = toTarget.normalized * MaxSpeed;
        velocity = Vector3.Lerp(velocity, desiredVelocity, SteerRate);
        // If this step would land on or past the target, snap exactly to
        // it (so the final frame hits the torso center) and destroy.
        float stepLen = velocity.magnitude;
        if (stepLen >= dist)
        {
            exactPosition = targetPos.Value;
            Destroy();
            return;
        }
        exactPosition += velocity;
        // Point the sprite along the direction of travel so non-round
        // textures (e.g. SparkThrown) read as streaking, not tumbling.
        if (velocity.sqrMagnitude > 0.0001f)
            exactRotation = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
    }

    private Vector3? ResolveTargetPos()
    {
        if (homingTarget == null) return null;
        if (homingTarget.Spawned && !homingTarget.Dead)
            return homingTarget.DrawPos;
        // Android was the killer, so losing the source mid-stream is an
        // unlikely edge case. Follow the corpse if it exists rather than
        // leaving particles orbiting the last known position.
        var corpse = homingTarget.Corpse;
        if (corpse != null && corpse.Spawned)
            return corpse.DrawPos;
        return null;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref homingTarget, "homingTarget");
        Scribe_Values.Look(ref velocity, "velocity");
    }
}
