using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Shared maintenance for the reactor core glow mote. Each reactor hediff owns
// a Mote reference and calls Maintain() from its tick path; this helper
// (re)creates the mote when missing, applies per-pawn body-size scaling, and
// drives colour/alpha each tick.
//
// Why a mote instead of another PawnRenderNode: render nodes draw at the
// pawn's altitude band and are occluded by the unnatural-darkness section
// layer. The mote at AltitudeLayer.Darkness draws above that overlay (the
// same trick CompNoctolEyes uses for noctol eye glow). In normal lighting
// the mote renders as an additive layer over the chest body attachment;
// in unnatural darkness the body is hidden but the glow punches through.
//
// Sizing parity: the mote texture is 256x256 with a 40px hot core and a
// baked halo gradient filling the remaining canvas. The body attachment is
// 128x128 with a 40px core. The mote def's drawSize is tuned empirically
// (not by pure math) — PawnRenderNodeWorker_AttachmentBody composes scale
// factors beyond bodyGraphicScale (chest-specific drawData scale, root
// pawn graphic scale, etc.) that we don't replicate here, so the cleanest
// path is matching by eye against the chest core on a reference pawn.
// The bodyGraphicScale average is still applied via Mote.Scale so the
// glow tracks body-type variation (Thin/Hulk/Fat).
//
// Positional parity: the body attachment renders with a downward offset
// onto the chest, not at the pawn's pivot. ChestOffset replicates this so
// the mote's hot center sits on the chest core rather than the pawn's
// vertical centre. Negative Z = south on screen.
//
// Facing parity: south-only, matching the reactor chest body attachment's
// visibleFacing. The reactor is inset into the chest and only visible from
// the front; its glow shouldn't show through the body from the back/sides.
// Implemented as alpha=0 rather than destroy/recreate so a facing change
// fades in/out without allocation churn. For non-standing postures the
// facing is read from PawnRenderer.LayingFacing (matches how
// MoteAttached gates attached-to-head visibility) so the glow correctly
// hides on a face-down / face-away prone body.
//
// Posture tracking: positioning through laying / bed / downed postures is
// handled by Mote_ReactorGlow, which mirrors the body's draw
// position and rotation each tick. This helper just seeds the chest offset
// onto the mote at creation time and drives the per-tick colour/alpha.
//
// Call from Hediff.Tick — NOT Hediff.TickInterval. In 1.6, Thing.DoTick
// batches TickInterval at UpdateRateTicks (variable, larger when the pawn
// is offscreen), which can exceed the mote's solidTime=600 and cause the
// mote to despawn between maintenance calls. Hediff.Tick fires every game
// tick regardless of distance — the same cadence CompNoctolEyes uses via
// CompTick for the noctol eye glow.
public static class ReactorGlowMote
{
    // Empirically-tuned chest offset. Pawn DrawPos sits at the cell centre /
    // head area; the chest body attachment renders lower than that, so the
    // mote needs a small southward (-Z) shift to overlay the chest core.
    private static readonly Vector3 ChestOffset = new(0f, 0f, -0.008f);

    private static ThingDef MoteDef => AAH_ThingDefOf.Mote_AAHReactorGlow;

    public static void Maintain(Pawn pawn, HediffDef reactorDef, ref Mote mote, Color tint)
    {
        if (pawn?.Spawned != true || MoteDef == null) return;

        // Master gate: when this reactor's body attachment is turned off the whole
        // visual is hidden (chassis + glow node + this mote), so tear the mote down.
        // The mote-overlay setting is the finer gate on top of that — when it's off
        // we likewise tear down so only the always-on render node remains. Either
        // way the teardown is live (flipping the setting takes effect immediately).
        bool moteEnabled = ReactorGlow.AttachmentsEnabledFor(reactorDef)
            && (ArchotechAndroidHardwareMod.Settings?.reactorGlowMoteOverlay ?? false);
        if (!moteEnabled)
        {
            if (mote is { Destroyed: false }) mote.Destroy();
            mote = null;
            return;
        }

        // Alpha fades with the android's power level (when the setting is on);
        // ReactorGlow.GlowOpacity is the single source of truth shared with the
        // render-node path. Cheap enough to read every tick — no throttling.
        float alpha = ReactorGlow.GlowOpacity(pawn);
        bool visible = alpha > 0f && IsChestVisible(pawn);

        float scale = 1f;
        if (pawn.story?.bodyType != null)
        {
            Vector2 s = pawn.story.bodyType.bodyGraphicScale;
            scale = (s.x + s.y) / 2f;
        }

        if (mote?.Destroyed != false)
        {
            if (!visible) return;
            // Pass Vector3.zero to the link offset — the chest offset is applied
            // per-tick in Mote_ReactorGlow.TimeInterval (rotated by BodyAngle).
            // Baking it into link1 would re-apply it as an axis-aligned offset
            // even when the body is rotated, which is what we're fixing.
            mote = MoteMaker.MakeAttachedOverlay(pawn, MoteDef, Vector3.zero, scale);
            if (mote == null) return;
            if (mote is Mote_ReactorGlow reactorMote)
                reactorMote.chestOffset = ChestOffset;
        }

        // Reapply scale every tick rather than only at creation: lets the mote
        // track body-type changes mid-save (mods like Character Editor / Body
        // Change can swap bodyType at runtime). Mote.Scale is a single Vector3
        // assignment — no allocation, safe to write every tick.
        mote.Scale = scale;
        mote.instanceColor = new Color(tint.r, tint.g, tint.b, visible ? alpha : 0f);
        mote.Maintain();
    }

    // Whether the reactor chest core is presented to the camera and should glow.
    // The reactor is inset into the front of the torso, so the glow is only ever
    // visible from the south face of the body, and only when the body is actually
    // drawn and unobstructed by what the pawn is carrying.
    private static bool IsChestVisible(Pawn pawn)
    {
        // Crawling: the torso is face-down against the ground, so the front-set
        // reactor never faces the camera. BodyAngle still rotates the body, but
        // the chest is pressed to the floor — hide unconditionally.
        if (pawn.Crawling) return false;

        // Being carried: we don't reconstruct the carrier-relative body transform
        // (70/290 + carrier angle), and a carried pawn's chest glow would punch
        // through the carrier's body at renderQueue 4000. Hide rather than misplace.
        if (pawn.CarriedBy != null || pawn.ParentHolder is Pawn_CarryTracker) return false;

        // Carrying a thing: the held item/pawn renders in front of the carrier
        // when facing south — which is the only facing the glow is visible at —
        // so it would always cover the chest. Hide while carrying anything.
        if (pawn.carryTracker?.CarriedThing != null) return false;

        // Vanilla AnimationDef playing: catches Anomaly-driven sequences
        // (Revenant, Shambler, Devourer, DeathRefusal, UnnaturalCorpseAwoken,
        // HoldingPlatformPawns) plus any modded AnimationDefs that go through
        // the standard render-tree animation system. The render tree applies
        // per-node body-root translations we don't mirror, so we degrade by
        // hiding. Note: Yayo's Animation also bails on HasAnimation internally,
        // so this gate is mutually respected — no double-handling.
        if (pawn.Drawer?.renderer?.HasAnimation == true) return false;

        // Melee Animation: bespoke render replacement via AM.AnimRenderer that
        // doesn't expose body-pos / body-angle through canonical accessors, so
        // we can't piggyback the way we do for Yayo's. We CAN ask MA whether
        // the pawn is currently being driven by an AnimRenderer
        // (PatchMaster.GetAnimator(pawn) != null) and hide if so. Reflection-
        // resolved once; no-op if MA isn't installed.
        if (IsMeleeAnimating(pawn)) return false;

        // Body not drawn (duty override hiding the body, or a bed that hides the
        // sleeper) — mirror PawnRenderer so the glow doesn't float over nothing.
        if (!ShowsBody(pawn)) return false;

        // Facing: standing uses pawn.Rotation, non-standing uses LayingFacing()
        // so a prone pawn rolled onto their back/side correctly hides the glow.
        var posture = pawn.GetPosture();
        Rot4 facing = posture == PawnPosture.Standing
            ? pawn.Rotation
            : (pawn.Drawer?.renderer?.LayingFacing() ?? pawn.Rotation);
        return facing == Rot4.South;
    }

    // Mirrors the body-visibility branches PawnRenderer uses (duty
    // drawBodyOverride and bed_showSleeperBody). Standing always shows the body.
    private static bool ShowsBody(Pawn pawn)
    {
        if (pawn.GetPosture() == PawnPosture.Standing) return true;

        bool? dutyOverride = pawn.mindState?.duty?.def?.drawBodyOverride;
        if (dutyOverride.HasValue) return dutyOverride.Value;

        var bed = pawn.CurrentBed();
        if (bed != null && pawn.RaceProps.Humanlike)
            return bed.def.building.bed_showSleeperBody;

        return true;
    }

    // Reflection cache for AM.Patches.PatchMaster.GetAnimator. Resolved once
    // per session; if Melee Animation isn't installed (or renames its API) the
    // method ref is null and IsMeleeAnimating short-circuits to false.
    private static System.Reflection.MethodInfo _meleeAnimGetAnimator;
    private static bool _meleeAnimResolved;
    private static readonly object[] _meleeAnimArgs = new object[1];

    private static bool IsMeleeAnimating(Pawn pawn)
    {
        if (!_meleeAnimResolved)
        {
            _meleeAnimResolved = true;
            var type = AccessTools.TypeByName("AM.Patches.PatchMaster");
            if (type != null)
                _meleeAnimGetAnimator = AccessTools.Method(type, "GetAnimator", new[] { typeof(Pawn) });
        }
        if (_meleeAnimGetAnimator == null) return false;
        _meleeAnimArgs[0] = pawn;
        return _meleeAnimGetAnimator.Invoke(null, _meleeAnimArgs) != null;
    }
}
