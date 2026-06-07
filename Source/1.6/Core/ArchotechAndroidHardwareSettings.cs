using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Render pipeline for the reactor core glow.
///
/// MoteExperimental — a Mote_AAHReactorGlow drawn at AltitudeLayer.Darkness /
///   renderQueue 4000, so it punches through the unnatural-darkness section
///   layer (the impressive effect). A mote isn't part of the pawn's render
///   tree, so Mote_ReactorGlow + ReactorGlowMote manually mirror the body's
///   posture/bed transform and hide the glow in cases they can't track
///   (crawling, carry, hidden body, vanilla animations). Because that render
///   order draws above almost everything, the glow can also punch through other
///   overlays (weapons, stun stars, combat effects), which some players may
///   find jarring. Third-party draw-patch animators (Yayo's Animations etc.)
///   can't be detected either, so it may also rarely misalign under heavy
///   animation mods — hence "experimental".
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

/// <summary>
/// Mod settings for the reactor balance knobs and render options.
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
public class ArchotechAndroidHardwareSettings : ModSettings
{
    public float thanaticRefillAmount = 0.35f;
    public float thanaticOverchargeHoursPerUnit = 17f;
    public float thanaticOverchargeCapHours = 48f;
    // VPE only, default off. Toggles the startup costList rewrite in
    // ViolenceGeneratorSalvageOverride (takes effect on restart).
    public bool overrideViolenceGeneratorSalvage = false;
    public ReactorGlowMode reactorGlowMode = ReactorGlowMode.MoteExperimental;
    public bool scaleReactorGlowByPower = true;

    // Psychic transceiver reprogramming unlock: when on (default), an awakened
    // android with the AAH_PsychicTransceiver implant permanently accepts
    // reprogramming at VREA's behavior station (which it would otherwise refuse).
    // The reliable, prerequisite-gated counterpart to the random inspiration —
    // the implant opens the android to outside (archotech) influence. Off makes
    // the transceiver a pure psychic-sensitivity implant with no station effect.
    public bool enableTransceiverReprogramming = true;

    // Self-Determination inspiration: lets an awakened android voluntarily
    // reprogram its subroutines at VREA's behavior station for one session.
    // enable — master toggle for granting the inspiration at all.
    // allowForAllAwakened — when off (default), only awakened androids carrying
    //   an AAH part are eligible (keeps the feature in our mod's lane); when on,
    //   any awakened colonist android can roll it.
    // commonality — random-pool weight; consumed by InspirationWorker.CommonalityFor.
    public bool enableSelfDeterminationInspiration = true;
    public bool allowSelfDeterminationForAllAwakened = false;
    public float selfDeterminationCommonality = 3f;

    // Transient UI state for the scrollable settings panel — not serialized.
    private Vector2 settingsScroll;
    private float settingsHeight;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref thanaticRefillAmount, "thanaticRefillAmount", 0.35f);
        Scribe_Values.Look(ref thanaticOverchargeHoursPerUnit, "thanaticOverchargeHoursPerUnit", 17f);
        Scribe_Values.Look(ref thanaticOverchargeCapHours, "thanaticOverchargeCapHours", 48f);
        Scribe_Values.Look(ref overrideViolenceGeneratorSalvage, "overrideViolenceGeneratorSalvage", false);
        Scribe_Values.Look(ref reactorGlowMode, "reactorGlowMode", ReactorGlowMode.MoteExperimental);
        Scribe_Values.Look(ref scaleReactorGlowByPower, "scaleReactorGlowByPower", true);
        Scribe_Values.Look(ref enableTransceiverReprogramming, "enableTransceiverReprogramming", true);
        Scribe_Values.Look(ref enableSelfDeterminationInspiration, "enableSelfDeterminationInspiration", true);
        Scribe_Values.Look(ref allowSelfDeterminationForAllAwakened, "allowSelfDeterminationForAllAwakened", false);
        Scribe_Values.Look(ref selfDeterminationCommonality, "selfDeterminationCommonality", 3f);
    }

    public void ResetToDefaults()
    {
        thanaticRefillAmount = 0.35f;
        thanaticOverchargeHoursPerUnit = 17f;
        thanaticOverchargeCapHours = 48f;
        overrideViolenceGeneratorSalvage = false;
        reactorGlowMode = ReactorGlowMode.MoteExperimental;
        scaleReactorGlowByPower = true;
        enableTransceiverReprogramming = true;
        enableSelfDeterminationInspiration = true;
        allowSelfDeterminationForAllAwakened = false;
        selfDeterminationCommonality = 3f;
    }

    public void DoWindowContents(Rect inRect)
    {
        const float buttonHeight = 30f;
        const float buttonGap = 10f;

        // Reserve a row at the bottom for the reset button; everything above
        // scrolls. settingsHeight (set at the end of the previous frame) drives
        // the scrollable content height so the view grows as we add settings.
        Rect viewRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - buttonHeight - buttonGap);
        Rect buttonRect = new Rect(inRect.x, inRect.yMax - buttonHeight, 200f, buttonHeight);

        float innerWidth = viewRect.width - 16f;
        Rect innerRect = new Rect(0f, 0f, innerWidth, Mathf.Max(settingsHeight, viewRect.height));
        Widgets.BeginScrollView(viewRect, ref settingsScroll, innerRect);

        var listing = new Listing_Standard();
        listing.Begin(new Rect(0f, 0f, innerWidth - 8f, 99999f));
        GameFont prevFont = Text.Font;

        listing.Gap();

        // ===== Reactors =====
        SectionHeader(listing, "Reactors");

        listing.Label("Reactor core glow render mode:");
        listing.Gap(6f);
        DrawGlowModeOption(listing, ReactorGlowMode.RenderNodeSafe,
            "Body attachment (reliable)",
            "Perfectly tracks the torso with conventional render ordering, but is occluded by unnatural darkness, weapons, and most other overlay effects.");
        DrawGlowModeOption(listing, ReactorGlowMode.MoteExperimental,
            "Mote overlay (experimental)",
            "Punches through night and unnatural darkness, but may also render over other overlays (weapons, stun text, other weather effects) which some players may find jarring. Rarely, it may also misalign during other animations.");

        listing.Gap(6f);
        listing.CheckboxLabeled("Dim reactor glow with power level",
            ref scaleReactorGlowByPower,
            "When enabled, the reactor core glow fades as the android's stored power " +
            "drops and shines at full strength when fully charged. When disabled, the " +
            "glow stays at a constant brightness.");

        listing.Gap(30f);

        // ===== Thanatic Reactor =====
        SectionHeader(listing, "Thanatic Reactor");

        listing.Label($"Refill per humanlike kill: {thanaticRefillAmount:F2} (fraction of max energy)");
        thanaticRefillAmount = listing.Slider(thanaticRefillAmount, 0.05f, 1.0f);

        listing.Gap(10f);
        listing.Label($"Thanatic Overcharge hours per unit of overflow: {thanaticOverchargeHoursPerUnit:F1}h");
        thanaticOverchargeHoursPerUnit = listing.Slider(thanaticOverchargeHoursPerUnit, 1f, 48f);

        listing.Gap(10f);
        listing.Label($"Thanatic Overcharge duration cap: {thanaticOverchargeCapHours:F0}h");
        thanaticOverchargeCapHours = listing.Slider(thanaticOverchargeCapHours, 6f, 120f);

        // VPE only: optional override that makes the Archotech Violence Generator
        // salvageable for reactors via plain vanilla deconstruction. Gated on the
        // generator def existing so the option never appears without VPE. The
        // actual costList rewrite happens at startup in ViolenceGeneratorSalvageOverride.
        if (DefDatabase<ThingDef>.GetNamedSilentFail("VPE_ArchotechViolenceGenerator") != null)
        {
            listing.Gap(18f);
            listing.CheckboxLabeled("Salvage reactors by deconstructing Archotech Violence Generators",
                ref overrideViolenceGeneratorSalvage,
                "When enabled, deconstructing a Vanilla Power Expanded Archotech Violence Generator " +
                "through normal vanilla means returns 250 steel and 3 thanatic reactors instead of its " +
                "usual salvage — letting you recover reactors without running the crafting bill.\n\n" +
                "Takes effect on game restart. (Shown only while Vanilla Power Expanded is installed.)");
        }

        listing.Gap(30f);

        // ===== Awakened Reprogramming =====
        SectionHeader(listing, "Awakened Reprogramming");

        listing.CheckboxLabeled("Psychic transceiver re-enables reprogramming",
            ref enableTransceiverReprogramming,
            "When enabled, an awakened android with a psychic transceiver installed will accept " +
            "reprogramming at an android behavior station, which it would otherwise refuse — the " +
            "implant's psychic bridge leaves it open to outside direction. This is the reliable, " +
            "permanent counterpart to the random Self-Determination inspiration below. Reprogramming " +
            "an android this way leaves it with a lingering unease that fades over the following weeks.\n\n" +
            "When disabled, the transceiver is purely a psychic-sensitivity implant with no effect " +
            "on the behavior station.");

        listing.Gap(18f);

        listing.CheckboxLabeled("Enable the Self-Determination inspiration",
            ref enableSelfDeterminationInspiration,
            "When enabled, awakened androids can occasionally gain the Self-Determination " +
            "inspiration. While it is active, the android will, for once, accept reprogramming " +
            "at an android behavior station despite having awakened, letting you change its " +
            "subroutines. Completing the reprogramming consumes the inspiration and grants a " +
            "positive mood memory.");

        if (enableSelfDeterminationInspiration)
        {
            listing.Gap(6f);
            listing.CheckboxLabeled("Offer it to all awakened androids",
                ref allowSelfDeterminationForAllAwakened,
                "When off (default), only awakened androids carrying an Archotech Android Hardware " +
                "part can gain this inspiration, which keeps it scoped to this mod. When on, any " +
                "awakened colonist android is eligible.");

            listing.Gap(10f);
            listing.Label($"Inspiration commonality (random-pool weight): {selfDeterminationCommonality:F1}");
            selfDeterminationCommonality = listing.Slider(selfDeterminationCommonality, 0.1f, 10f);

            // Succinct reference scale: this is a selection weight (which inspiration
            // gets picked), not how often inspirations occur. Vanilla inspirations are
            // all baseCommonality 1, scaled by passion (x1 / x2.5 / x5).
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            listing.Label("Weight when picked among the pawn's eligible inspirations. " +
                "Reference: 1 plain vanilla, 2.5 vanilla minor passion, 5 vanilla major passion. " +
                "Default 3, just above minor passion.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        listing.Gap(60f);

        Text.Font = prevFont;
        settingsHeight = listing.CurHeight;
        listing.End();
        Widgets.EndScrollView();

        if (Widgets.ButtonText(buttonRect, "Reset to defaults"))
            ResetToDefaults();
    }

    /// <summary>
    /// Renders one option of the reactor-glow radio group: a short label with
    /// the explanation in the hover tooltip. Indented under the prompt so the
    /// options read as children of the setting, with a trailing 6px gap.
    /// </summary>
    private void DrawGlowModeOption(Listing_Standard listing, ReactorGlowMode mode, string label, string tooltip)
    {
        if (listing.RadioButton(label, reactorGlowMode == mode, tabIn: 16f, tooltip: tooltip))
            reactorGlowMode = mode;
        listing.Gap(6f);
    }

    /// <summary>Top-level section heading (medium font), e.g. "Reactors".</summary>
    private static void SectionHeader(Listing_Standard listing, string label)
    {
        Text.Font = GameFont.Medium;
        listing.Label(label);
        Text.Font = GameFont.Small;
        listing.Gap(10f);
    }
}
