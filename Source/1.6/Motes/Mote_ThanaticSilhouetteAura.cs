using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// MoteAttached carrying per-instance scroll-speed overrides for the
/// MoteMultiplyAddScroll shader layers. The Graphic_PawnBodySilhouette
/// postfix reads these fields each draw and pushes them onto the cached
/// body/head materials, overriding the XML-baked defaults.
///
/// When <see cref="overrideScroll"/> is false, the postfix no-ops and the
/// mote behaves exactly like a plain MoteAttached — same defName can be
/// reused for non-directional auras (e.g. the source-pawn aura spawned by
/// the delayed timer).
///
/// Scroll vectors are world-space directions — MoteMultiplyAddScroll
/// samples in world space (via _pawnCenterWorld), so mesh rotation does
/// not affect the visual flow direction. Visual pattern motion is
/// opposite to the scroll vector (sample drift +x → pattern appears to
/// move -x), so callers set scroll = -W * speed to produce visual flow
/// toward world direction W. See SpawnDirectionalVictimAura in
/// Hediff_ThanaticReactor for the concrete wiring.
/// </summary>
public class Mote_ThanaticSilhouetteAura : MoteAttached
{
    public bool overrideScroll;
    public Vector2 texAScroll;
    public Vector2 texBScroll;
    public Vector2 detailScroll;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref overrideScroll, "overrideScroll");
        Scribe_Values.Look(ref texAScroll, "texAScroll");
        Scribe_Values.Look(ref texBScroll, "texBScroll");
        Scribe_Values.Look(ref detailScroll, "detailScroll");
    }
}
