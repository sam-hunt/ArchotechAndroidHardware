using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Mod settings for the reactor balance knobs and render options.
///
/// Reactor core glow rendering — two layers:
///   • Body-attachment render node (PawnRenderNodeWorker_ReactorGlow) — ALWAYS
///     drawn. As a sibling of the chest attachment it inherits every body
///     transform automatically (posture, bed, carry, crawl, vanilla + third-party
///     animation) and also appears anywhere the pawn is portrait-rendered (the
///     colonist bar, the inspect-pane icon). Trade-off: it draws in the pawn's
///     altitude band, so unnatural darkness occludes it.
///   • Mote overlay (ReactorGlowMote → Mote_AAHReactorGlow) — OPTIONAL, gated by
///     the reactorGlowMoteOverlay setting. Drawn at AltitudeLayer.Darkness /
///     renderQueue 4000 so it punches through night and unnatural darkness (the
///     CompNoctolEyes trick). It is layered ON TOP of the render node, not in
///     place of it. Because that render order draws above almost everything it
///     can also cover other overlays (weapons, stun text, weather), and a mote
///     isn't in the render tree so ReactorGlowMote mirrors the body transform by
///     hand and hides in cases it can't track (crawling, carry, hidden body,
///     animations) — hence it's the opt-in extra rather than the baseline.
/// The setting switches live: ReactorGlowMote.Maintain creates/keeps the mote
/// only while reactorGlowMoteOverlay is on and tears it down when flipped off.
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
///     Overcharge duration stacks via HediffComp_Disappears extension, capped at
///     thanaticOverchargeCapHours (default 48h; the slider's top notch removes the cap)
///
/// Design intent: most raids should push Energy into overcharge territory
/// rather than forcing players to hunt for kills to survive. If the realised
/// drain rate undershoots that target, either raise the refill knob or
/// increase the gene's biostatMet magnitude (closer to 0 = slower drain).
///
///   Grav Overcharge characteristics:
///     A gravship landing refills the grav reactor by gravRefillAmount
///     (default 0.5 = half a tank). Refill that would push past max overflows
///     and discharges as the Grav Overcharge buff (cold tolerance + move/work
///     speed; stat envelope in HediffDefs_GravOvercharge.xml) — so overflow
///     appears only when the reactor was already more than (1 - refill) full at
///     launch, and a fuller reactor grants more overcharge.
///     gravOverchargeHoursPerUnit = buff hours per unit of overflow; since a
///     full-meter launch overflows by exactly `refill`, the most a single launch
///     can grant is refill × hoursPerUnit (default 0.5 × 24h = 12h). Duration
///     stacks across launches, capped at gravOverchargeCapHours (default 72h /
///     3 days; slider runs to 7 days, then a top notch = ThanaticOverchargeCapUnlimited
///     analogue removes the cap). No psyfocus bump — that is thanatic's psychic
///     theme. See Hediff_GravOvercharge.
///     (Deferred: scaling refill / overcharge by gravship trip distance.)
/// </summary>
public class ArchotechAndroidHardwareSettings : ModSettings
{
    public float thanaticRefillAmount = 0.35f;
    public float thanaticOverchargeHoursPerUnit = 17f;
    public float thanaticOverchargeCapHours = 48f;
    // Sentinel for thanaticOverchargeCapHours: the slider's top notch (one hour
    // past the 120h max of meaningful values). At this value the duration cap is
    // removed entirely — overflow kills stack without limit. Hediff_ThanaticOvercharge
    // treats any cap >= this as "no cap".
    public const float ThanaticOverchargeCapUnlimited = 121f;

    // Grav reactor refill / overcharge (Odyssey-gated UI; the fields persist
    // regardless so a save toggling Odyssey doesn't lose its tuning).
    // gravRefillAmount default 0.5 = a launch tops up half a tank; overflow only
    // spills into Grav Overcharge when the reactor was already more than
    // (1 - refill) full at launch. See the class docblock + Hediff_GravOvercharge.
    public float gravRefillAmount = 0.5f;
    public float gravOverchargeHoursPerUnit = 24f;
    public float gravOverchargeCapHours = 72f;
    // Sentinel mirroring ThanaticOverchargeCapUnlimited: the grav cap slider's
    // top notch (one hour past the 168h / 7-day meaningful max) removes the cap
    // entirely. Hediff_GravOvercharge treats any cap >= this as "no cap".
    public const float GravOverchargeCapUnlimited = 169f;

    // VPE only, default off. Toggles the startup costList rewrite in
    // ViolenceGeneratorSalvageOverride (takes effect on restart).
    public bool overrideViolenceGeneratorSalvage = false;
    // The body-attachment render node always draws the glow; this toggles the
    // additional darkness-piercing mote overlay layered on top (see class doc).
    public bool reactorGlowMoteOverlay = true;
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
        Scribe_Values.Look(ref gravRefillAmount, "gravRefillAmount", 0.5f);
        Scribe_Values.Look(ref gravOverchargeHoursPerUnit, "gravOverchargeHoursPerUnit", 24f);
        Scribe_Values.Look(ref gravOverchargeCapHours, "gravOverchargeCapHours", 72f);
        Scribe_Values.Look(ref overrideViolenceGeneratorSalvage, "overrideViolenceGeneratorSalvage", false);
        Scribe_Values.Look(ref reactorGlowMoteOverlay, "reactorGlowMoteOverlay", true);
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
        gravRefillAmount = 0.5f;
        gravOverchargeHoursPerUnit = 24f;
        gravOverchargeCapHours = 72f;
        overrideViolenceGeneratorSalvage = false;
        reactorGlowMoteOverlay = true;
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

        // TODO: Add setting "Render AAH reactor body attachments" (enabled by default)
        // TODO: Add setting "Render VREA reactor body attachment" (enabled by default)

        // TODO: Gate other reactor render settings in this section on at least one of the above being enabled

        listing.CheckboxLabeled("Render reactor glow mote (experimental)",
            ref reactorGlowMoteOverlay,
            "When enabled, an extra glow layer is rendered for the reactor core at a very high render order, " +
            "piercing night, and unnatural darkness. Because it draws above almost everything, it may also " +
            "render over some overlays (weapons, stun text, weather effects etc), which some players find jarring. " +
            "Rarely, it may also misalign during heavy animation or if the pawn moves between game ticks.");

        listing.Gap(6f);
        listing.CheckboxLabeled("Scale reactor glow opacity by power need level",
            ref scaleReactorGlowByPower,
            "When enabled, the reactor core glows at full strength when the power need meter is full, and dims " +
            "as the level drops. When disabled, the glow stays at a constant full brightness.");

        listing.Gap(30f);

        // ===== Thanatic Reactor =====
        SectionHeader(listing, "Thanatic Reactor");

        listing.Label($"Refill per humanlike kill: {thanaticRefillAmount:F2} (fraction of max energy)");
        thanaticRefillAmount = listing.Slider(thanaticRefillAmount, 0.05f, 1.0f);

        // TODO: Add setting for refill per non-humanlike kill (mechs, animals, entities etc)

        listing.Gap(10f);
        listing.Label($"Thanatic Overcharge hours per kill when power meter is full: {thanaticOverchargeHoursPerUnit:F1}h");
        thanaticOverchargeHoursPerUnit = listing.Slider(thanaticOverchargeHoursPerUnit, 1f, 48f);

        // TODO: Add setting to scale power refill fraction and overcharge hours by victim psychic sensitivity

        listing.Gap(10f);
        bool capUnlimited = thanaticOverchargeCapHours >= ThanaticOverchargeCapUnlimited;
        listing.Label(capUnlimited
            ? "Thanatic Overcharge duration cap: Unlimited (kills stack without limit)"
            : $"Thanatic Overcharge duration cap: {thanaticOverchargeCapHours:F0}h");
        // The extra notch past 120h (ThanaticOverchargeCapUnlimited) removes the cap.
        // Round so the stored value snaps to whole hours, making that top notch a
        // deterministic sentinel rather than a near-max float that reads as "121h".
        thanaticOverchargeCapHours = Mathf.Round(
            listing.Slider(thanaticOverchargeCapHours, 6f, ThanaticOverchargeCapUnlimited));

        // VFEPower only: optional override that makes the Archotech Violence Generator
        // salvageable for reactors via plain vanilla deconstruction. Gated on the
        // generator def existing so the option never appears without VPE. The
        // actual costList rewrite happens at startup in ViolenceGeneratorSalvageOverride.
        if (AAH_ThingDefOf.VPE_ArchotechViolenceGenerator != null)
        {
            listing.Gap(18f);
            listing.CheckboxLabeled("Replace Archotech Violence Generator costList",
                ref overrideViolenceGeneratorSalvage,
                "When enabled, deconstructing a VFEPower Archotech Violence Generator returns 250 steel " +
                "and 3 thanatic reactors instead of its usual 275 steel + 9 Advanced Components. " +
                "Takes effect on game restart.");
        }

        listing.Gap(30f);

        // ===== Grav Reactor (Odyssey only) =====
        // The grav reactor, its refill, and its overcharge are all driven by the
        // gravship launch mechanic, so the whole section is hidden without Odyssey.
        if (ModsConfig.OdysseyActive)
        {
            SectionHeader(listing, "Grav Reactor");

            listing.Label($"Refill per grav launch: {gravRefillAmount:F2} (fraction of max energy)");
            gravRefillAmount = listing.Slider(gravRefillAmount, 0.05f, 1.0f);

            listing.Gap(10f);
            listing.Label($"Grav Overcharge hours per launch when power meter is full: {gravOverchargeHoursPerUnit:F1}h");
            gravOverchargeHoursPerUnit = listing.Slider(gravOverchargeHoursPerUnit, 1f, 48f);

            listing.Gap(10f);
            bool gravCapUnlimited = gravOverchargeCapHours >= GravOverchargeCapUnlimited;
            // Label reads in hours up to 72h, then switches to days (units change
            // from hours to days after 72h); the top notch is "Unlimited".
            string gravCapText = gravCapUnlimited
                ? "Unlimited (launches stack without limit)"
                : gravOverchargeCapHours > 72f
                    ? $"{gravOverchargeCapHours / 24f:F1} days"
                    : $"{gravOverchargeCapHours:F0}h";
            listing.Label($"Grav Overcharge duration cap: {gravCapText}");
            // Sentinel scheme matches the Thanatic cap: the notch past the 168h
            // (7-day) meaningful max (GravOverchargeCapUnlimited) removes the cap.
            // Slider keeps whole-hour granularity (Mathf.Round) so only the label
            // unit changes, not the step size, and the top notch lands on the
            // sentinel int deterministically rather than a near-max float.
            gravOverchargeCapHours = Mathf.Round(
                listing.Slider(gravOverchargeCapHours, 6f, GravOverchargeCapUnlimited));

            // TODO: Scale refill fraction and overcharge hours by gravship trip
            // distance (straight-line tiles between the controller's takeoff /
            // landing tiles). Needs a normalization curve + a "reference distance
            // for full effect" knob. Deferred.

            listing.Gap(30f);
        }

        // ===== Psychic Transceiver =====
        SectionHeader(listing, "Psychic Transceiver");

        listing.CheckboxLabeled("Psychic transceiver re-enables reprogramming",
            ref enableTransceiverReprogramming,
            "When enabled, an awakened android with a psychic transceiver installed will accept " +
            "reprogramming at an android behavior station, which it would otherwise refuse: the " +
            "implant's psychic bridge leaves it open to outside influence. Reprogramming " +
            "an android this way leaves it with a lingering unease that fades over the following weeks.\n\n" +
            "When disabled, the transceiver is purely a psychic-sensitivity implant with no effect " +
            "on the behavior station.");

        listing.Gap(30f);

        // ===== Miscellaneous =====
        SectionHeader(listing, "Miscellaneous");

        // TODO: Refactor the 3 inspiration-related settings below to combine the 2x checkboxes + 1x slider into 2x sliders:
        // one for androids with >= 1 AAH part, and one for androids without.
        // default for those with AAH parts should be 3.0, recommended 3.0
        // default for those without should be 0.0 (for parity with VREA balance), recommended 1.0
        listing.CheckboxLabeled("Enable Self-Determination inspiration",
            ref enableSelfDeterminationInspiration,
            "When enabled, awakened androids with archotech android hardware may occasionally gain " +
            "an inspiration during which it will accept subroutine reprogramming at an android behavior " +
            "Completing the reprogramming consumes the inspiration and grants a small positive moodlet.");

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
            // TODO: Move the annotations for each value here to parenthesized suffixes on the main slider's label
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

    /// <summary>Top-level section heading (medium font), e.g. "Reactors".</summary>
    private static void SectionHeader(Listing_Standard listing, string label)
    {
        Text.Font = GameFont.Medium;
        listing.Label(label);
        Text.Font = GameFont.Small;
        listing.Gap(10f);
    }
}
