using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Mod settings for the Thanatic Reactor balance knobs.
///
/// Balance rationale (VREA 1.6, verified at implementation time — re-verify if
/// VREA internals change):
///
///   Baseline VREA reactor drain (Hediff_AndroidReactor.TickInterval):
///     per-check drain = 1.388889e-7 × PowerEfficiencyDrainMultiplier × 60
///                     ≈ 8.333e-6 energy per 60-tick interval
///     per-day drain (met=0) ≈ 0.008333 → full→empty in ~120 days
///
///   PowerEfficiencyToPowerDrainFactorCurve (AndroidStatsTable) — anchors we've
///   observed in VREA 1.6:
///     biostatMet sum -20 → 6.0× drain
///     biostatMet sum   0 → 1.0× drain (baseline)
///     biostatMet sum  +5 → 0.5× drain
///   The curve shape between anchors is VREA's, not ours, so intermediate
///   multipliers shouldn't be asserted as specific numbers here.
///
///   Thanatic Reactor characteristics:
///     The AAH_ThanaticReactor gene contributes biostatMet -4. That shifts
///     the pawn's sum into an accelerated-drain region of VREA's curve —
///     somewhere between 1.0× (met 0) and 6.0× (met -20) — but the realised
///     multiplier depends on VREA's curve and is the tuning surface (via the
///     gene's biostatMet) rather than a constant we ship here.
///     No separate drain multiplier is applied in C#; all drain scaling
///     happens through VREA's native biostatMet → curve path.
///     ThanaticRefillAmount = 0.35 → each humanlike kill refills 35% of max
///     energy. The sustain cadence (kills per day) depends on the realised
///     drain rate; adjust via the refill knob if the default curve output
///     makes it too tight or too generous.
///     Overflow: any kill while Energy > (1 - refill) overflows into
///     Thanatic Overcharge.
///     Max overcharge from a single kill (kill at Energy=1.0) = 0.35 × 17h ≈ 5h57m
///     Overcharge duration stacks via HediffComp_Disappears extension, capped at 48h
///
/// Design intent: most raids should push Energy into overcharge territory
/// rather than forcing players to hunt for kills to survive. If the realised
/// drain rate undershoots that target, either raise the refill knob or
/// increase the gene's biostatMet magnitude (closer to 0 = slower drain).
/// </summary>
/// <summary>
/// Render pipeline for the reactor core glow.
///
/// MoteExperimental — a Mote_AAHReactorGlow drawn at AltitudeLayer.Darkness /
///   renderQueue 4000, so it punches through the unnatural-darkness section
///   layer (the impressive effect). A mote isn't part of the pawn's render
///   tree, so Mote_ReactorGlow + ReactorGlowMote manually mirror the body's
///   posture/bed transform and hide the glow in cases they can't track
///   (crawling, carry, hidden body, vanilla animations). Third-party draw-patch
///   animators (Yayo's Animations etc.) can't be detected, so the glow may
///   misalign under heavy animation mods — hence "experimental".
///
/// RenderNodeSafe — the glow is a body-parented PawnRenderNode (sibling of the
///   chest attachment). It inherits every transform the body does automatically
///   (posture, bed, carry, crawl, vanilla + third-party animation), so it's
///   always correctly placed. Trade-off: it draws in the pawn's altitude band
///   and is occluded by unnatural darkness — no punch-through.
///
/// The paths are mutually exclusive and switch live: the render node's worker
/// (PawnRenderNodeWorker_ReactorGlow) only draws in RenderNodeSafe mode, and
/// ReactorGlowMote.Maintain only creates/keeps a mote in MoteExperimental mode
/// (tearing down any live mote when the setting flips).
/// </summary>
public enum ReactorGlowMode { MoteExperimental, RenderNodeSafe }

public class ArchotechAndroidHardwareSettings : ModSettings
{
    public float thanaticRefillAmount = 0.35f;
    public float thanaticOverchargeHoursPerUnit = 17f;
    public float thanaticOverchargeCapHours = 48f;
    public ReactorGlowMode reactorGlowMode = ReactorGlowMode.MoteExperimental;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref thanaticRefillAmount, "thanaticRefillAmount", 0.35f);
        Scribe_Values.Look(ref thanaticOverchargeHoursPerUnit, "thanaticOverchargeHoursPerUnit", 17f);
        Scribe_Values.Look(ref thanaticOverchargeCapHours, "thanaticOverchargeCapHours", 48f);
        Scribe_Values.Look(ref reactorGlowMode, "reactorGlowMode", ReactorGlowMode.MoteExperimental);
    }

    public void DoWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.Label($"Refill per humanlike kill: {thanaticRefillAmount:F2} (fraction of max energy)");
        thanaticRefillAmount = listing.Slider(thanaticRefillAmount, 0.05f, 1.0f);

        listing.Gap();
        listing.Label($"Thanatic Overcharge hours per unit of overflow: {thanaticOverchargeHoursPerUnit:F1}h");
        thanaticOverchargeHoursPerUnit = listing.Slider(thanaticOverchargeHoursPerUnit, 1f, 48f);

        listing.Gap();
        listing.Label($"Thanatic Overcharge duration cap: {thanaticOverchargeCapHours:F0}h");
        thanaticOverchargeCapHours = listing.Slider(thanaticOverchargeCapHours, 6f, 120f);

        listing.GapLine();
        listing.Label("Reactor core glow render mode:");
        if (listing.RadioButton(
                "Mote overlay (experimental) — glows through unnatural darkness; may misalign under heavy animation mods",
                reactorGlowMode == ReactorGlowMode.MoteExperimental))
            reactorGlowMode = ReactorGlowMode.MoteExperimental;
        if (listing.RadioButton(
                "Body attachment (safe) — always tracks the torso; occluded by unnatural darkness",
                reactorGlowMode == ReactorGlowMode.RenderNodeSafe))
            reactorGlowMode = ReactorGlowMode.RenderNodeSafe;

        listing.End();
    }
}
