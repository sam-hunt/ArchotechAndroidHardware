using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// "Thanatic Reactor" settings section.
///
///   Thanatic Reactor characteristics:
///     The AAH_ThanaticReactor gene contributes biostatMet -4. That shifts
///     the pawn's sum into an accelerated-drain region of VREA's curve —
///     somewhere between 1.0× (met 0) and 6.0× (met -20) — but the realised
///     multiplier depends on VREA's curve and is the tuning surface (via the
///     gene's biostatMet) rather than a constant we ship here.
///     No separate drain multiplier is applied in C#; all drain scaling
///     happens through VREA's native biostatMet → curve path. (See the shared
///     balance reference on <see cref="ArchotechAndroidHardwareSettings"/>.)
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
/// </summary>
public partial class ArchotechAndroidHardwareSettings
{
    public float thanaticRefillAmount = 0.35f;
    public float thanaticOverchargeHoursPerUnit = 17f;
    public float thanaticOverchargeCapHours = 48f;
    // Sentinel for thanaticOverchargeCapHours: the slider's top notch (one hour
    // past the 120h max of meaningful values). At this value the duration cap is
    // removed entirely — overflow kills stack without limit. Hediff_ThanaticOvercharge
    // treats any cap >= this as "no cap".
    public const float ThanaticOverchargeCapUnlimited = 121f;

    // VPE only, default off. Toggles the startup costList rewrite in
    // ViolenceGeneratorSalvageOverride (takes effect on restart).
    public bool overrideViolenceGeneratorSalvage = false;

    private void ExposeThanaticReactorSettings()
    {
        Scribe_Values.Look(ref thanaticRefillAmount, "thanaticRefillAmount", 0.35f);
        Scribe_Values.Look(ref thanaticOverchargeHoursPerUnit, "thanaticOverchargeHoursPerUnit", 17f);
        Scribe_Values.Look(ref thanaticOverchargeCapHours, "thanaticOverchargeCapHours", 48f);
        Scribe_Values.Look(ref overrideViolenceGeneratorSalvage, "overrideViolenceGeneratorSalvage", false);
    }

    private void ResetThanaticReactorSettings()
    {
        thanaticRefillAmount = 0.35f;
        thanaticOverchargeHoursPerUnit = 17f;
        thanaticOverchargeCapHours = 48f;
        overrideViolenceGeneratorSalvage = false;
    }

    private void DrawThanaticReactorSection(Listing_Standard listing)
    {
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
    }
}
