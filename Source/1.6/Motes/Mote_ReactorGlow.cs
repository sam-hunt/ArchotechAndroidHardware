using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Reactor core glow mote that tracks the pawn's body through non-standing
// postures (laying down, in bed, downed). A plain MoteAttached
// only follows link1.LastDrawPos (pawn pivot) and skips the posture/bed
// transforms that PawnRenderer applies before drawing the body,
// so when the pawn lies down the glow stays floating where the upright chest
// would have been. This subclass mirrors the body-resolution logic in
// PawnRenderer.GetBodyPos + PawnRenderer.BodyAngle and rotates
// chestOffset with the body so the hot core stays glued to the
// chest core, and rolls Mote.exactRotation with the torso so
// the halo rotates with the body too.
//
// Why subclass instead of swapping to a PawnRenderNode: render nodes draw at
// the pawn's altitude band and are occluded by the unnatural-darkness section
// layer. The mote draws at the transparent renderQueue and punches through
// (same trick CompNoctolEyes uses). See ReactorGlowMote for the full
// rationale on why this mote overlay exists alongside the always-on render node.
//
// Y coordinate is left alone after base TimeInterval — the mote's draw
// altitude is governed by the def's altitudeLayer + renderQueue, not by
// exactPosition.y, so overwriting only XZ keeps the existing punch-through
// behaviour while fixing the horizontal tracking.
//
// Yayo's Animation compatibility: we resolve the body position by reflection-
// calling the real PawnRenderer.GetBodyPos instead of mirroring its
// logic locally. Yayo's GetBodyPosPatch is a Postfix on that method
// that adds the per-pawn animation offset (pdd.posOffset) to the result, so
// Harmony runs Yayo's postfix on our reflection call too — the glow tracks
// Yayo's animated body for free. PawnRenderer.BodyAngle is already
// called publicly, and Yayo's BodyAnglePatch postfix on it gives us
// the animated angle the same way. (Yayo itself bails on HasAnimation,
// matching our vanilla-animation visibility gate, so no double-handling
// during Anomaly rituals etc.)
//
// If reflection ever fails (RimWorld renames the method, etc.) we fall back
// to a local mirror of GetBodyPos so positioning still works — just
// without Yayo's postfix contribution in that fallback case.
public class Mote_ReactorGlow : MoteAttached
{
    public Vector3 chestOffset;

    protected override void TimeInterval(float deltaTime)
    {
        base.TimeInterval(deltaTime);

        if (!link1.Linked) return;
        if (link1.Target.Thing is not Pawn pawn || !pawn.Spawned) return;

        var renderer = pawn.Drawer?.renderer;
        if (renderer == null) return;

        var posture = pawn.GetPosture();
        float bodyAngle = (posture == PawnPosture.Standing) ? 0f : renderer.BodyAngle(PawnRenderFlags.None);

        Vector3 bodyPos = ResolveBodyPos(pawn, posture);
        Vector3 rotatedOffset = chestOffset.RotatedBy(bodyAngle);

        // Preserve Y from base TimeInterval — see class doc.
        exactPosition = new Vector3(
            bodyPos.x + rotatedOffset.x,
            exactPosition.y,
            bodyPos.z + rotatedOffset.z);
        exactRotation = bodyAngle;
    }

    // Resolves the body's draw position, preferring the canonical
    // PawnRenderer.GetBodyPos (which carries every other mod's postfix
    // contributions — notably Yayo's per-pawn offset). Falls back to a local
    // mirror of the vanilla logic if the reflection lookup fails, so we
    // degrade gracefully rather than break.
    private static Vector3 ResolveBodyPos(Pawn pawn, PawnPosture posture)
    {
        var renderer = pawn.Drawer?.renderer;
        if (renderer != null && TryInvokeGetBodyPos(renderer, pawn.DrawPos, posture, out var result))
            return result;
        return MirrorGetBodyPos(pawn, posture);
    }

    // Reflection cache for the private PawnRenderer.GetBodyPos. Resolved once
    // per session; if RimWorld renames or removes the method we silently fall
    // back to the local mirror (preserving correctness without Yayo's offset).
    private static MethodInfo _getBodyPosMI;
    private static bool _getBodyPosResolved;

    private static bool TryInvokeGetBodyPos(PawnRenderer renderer, Vector3 drawLoc, PawnPosture posture, out Vector3 result)
    {
        if (!_getBodyPosResolved)
        {
            _getBodyPosResolved = true;
            _getBodyPosMI = AccessTools.Method(typeof(PawnRenderer), "GetBodyPos");
        }
        if (_getBodyPosMI == null)
        {
            result = default;
            return false;
        }
        // args[2] is the `out bool showBody` slot — we already gate showBody in
        // ReactorGlowMote.ShowsBody, so discard the out value here.
        var args = new object[] { drawLoc, posture, false };
        result = (Vector3)_getBodyPosMI.Invoke(renderer, args);
        return true;
    }

    // Fallback mirror of the non-private bits of PawnRenderer.GetBodyPos
    // for the case where reflection fails. Inputs are all public
    // (CurrentBed, BaseHeadOffsetAt, bodyType.bedOffset,
    // bed_pawnDrawOffset). Does not include Yayo's posOffset because
    // that's contributed via Harmony postfix on the real method.
    private static Vector3 MirrorGetBodyPos(Pawn pawn, PawnPosture posture)
    {
        if (posture == PawnPosture.Standing)
            return pawn.DrawPos;

        var bed = pawn.CurrentBed();
        if (bed != null && pawn.RaceProps.Humanlike)
        {
            Vector3 basePos = pawn.Position.ToVector3Shifted();
            Rot4 rot = bed.Rotation;
            rot.AsInt += 2;
            float shift = pawn.Drawer.renderer.BaseHeadOffsetAt(Rot4.South).z
                          + (pawn.story?.bodyType?.bedOffset ?? 0f)
                          + bed.def.building.bed_pawnDrawOffset;
            return basePos - rot.FacingCell.ToVector3() * shift;
        }

        return pawn.DrawPos;
    }
}
