using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Shared maintenance for the reactor core glow mote. Each reactor hediff owns
/// a Mote reference and calls Maintain() from its tick path; this helper
/// (re)creates the mote when missing, applies per-pawn body-size scaling, and
/// drives colour/alpha each tick.
///
/// Why a mote instead of another PawnRenderNode: render nodes draw at the
/// pawn's altitude band and are occluded by the unnatural-darkness section
/// layer. The mote at AltitudeLayer.Darkness draws above that overlay (the
/// same trick CompNoctolEyes uses for noctol eye glow). In normal lighting
/// the mote renders as an additive layer over the chest body attachment;
/// in unnatural darkness the body is hidden but the glow punches through.
///
/// Sizing parity: the mote texture is 256x256 with a 40px hot core and a
/// baked halo gradient filling the remaining canvas. The body attachment is
/// 128x128 with a 40px core. The mote def's drawSize is tuned empirically
/// (not by pure math) — PawnRenderNodeWorker_AttachmentBody composes scale
/// factors beyond bodyGraphicScale (chest-specific drawData scale, root
/// pawn graphic scale, etc.) that we don't replicate here, so the cleanest
/// path is matching by eye against the chest core on a reference pawn.
/// The bodyGraphicScale average is still applied via Mote.Scale so the
/// glow tracks body-type variation (Thin/Hulk/Fat).
///
/// Positional parity: the body attachment renders with a downward offset
/// onto the chest, not at the pawn's pivot. ChestOffset replicates this so
/// the mote's hot center sits on the chest core rather than the pawn's
/// vertical centre. Negative Z = south on screen.
///
/// Facing parity: south-only, matching the reactor chest body attachment's
/// visibleFacing. The reactor is inset into the chest and only visible from
/// the front; its glow shouldn't show through the body from the back/sides.
/// Implemented as alpha=0 rather than destroy/recreate so a facing change
/// fades in/out without allocation churn.
///
/// Call from Hediff.Tick — NOT Hediff.TickInterval. In 1.6, Thing.DoTick
/// batches TickInterval at UpdateRateTicks (variable, larger when the pawn
/// is offscreen), which can exceed the mote's solidTime=600 and cause the
/// mote to despawn between maintenance calls. Hediff.Tick fires every game
/// tick regardless of distance — the same cadence CompNoctolEyes uses via
/// CompTick for the noctol eye glow.
/// </summary>
public static class ReactorGlowMote
{
    // Empirically-tuned chest offset. Pawn DrawPos sits at the cell centre /
    // head area; the chest body attachment renders lower than that, so the
    // mote needs a small southward (-Z) shift to overlay the chest core.
    private static readonly Vector3 ChestOffset = new(0f, 0f, -0.008f);

    private static ThingDef _moteDefCache;
    private static ThingDef MoteDef =>
        _moteDefCache ??= DefDatabase<ThingDef>.GetNamed("Mote_AAHReactorGlow", errorOnFail: false);

    public static void Maintain(Pawn pawn, ref Mote mote, Color tint, float brightness)
    {
        if (pawn == null || !pawn.Spawned || MoteDef == null) return;

        bool visible = pawn.Rotation == Rot4.South && brightness > 0f;

        float scale = 1f;
        if (pawn.story?.bodyType != null)
        {
            Vector2 s = pawn.story.bodyType.bodyGraphicScale;
            scale = (s.x + s.y) / 2f;
        }

        if (mote == null || mote.Destroyed)
        {
            if (!visible) return;
            mote = MoteMaker.MakeAttachedOverlay(pawn, MoteDef, ChestOffset, scale);
            if (mote == null) return;
        }

        // Reapply scale every tick rather than only at creation: lets the mote
        // track body-type changes mid-save (mods like Character Editor / Body
        // Change can swap bodyType at runtime). Mote.Scale is a single Vector3
        // assignment — no allocation, safe to write every tick.
        mote.Scale = scale;
        mote.instanceColor = new Color(tint.r, tint.g, tint.b, visible ? brightness : 0f);
        mote.Maintain();
    }
}
