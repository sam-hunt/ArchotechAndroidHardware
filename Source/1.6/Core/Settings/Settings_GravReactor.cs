using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// "Grav Reactor" settings section (Odyssey only — the grav reactor, its refill,
// and its overcharge are all driven by the gravship launch mechanic, so the
// whole section is hidden without Odyssey; DrawGravReactorSection
// early-returns).
//
//   Grav Overcharge characteristics:
//     A gravship landing refills the grav reactor by gravRefillAmount
//     (default 0.5 = half a tank). Refill that would push past max overflows
//     and discharges as the Grav Overcharge buff (cold tolerance + move/work
//     speed; stat envelope in HediffDefs_GravOvercharge.xml) — so overflow
//     appears only when the reactor was already more than (1 - refill) full at
//     launch, and a fuller reactor grants more overcharge.
//     gravOverchargeHoursPerUnit = buff hours per unit of overflow; since a
//     full-meter launch overflows by exactly `refill`, the most a single launch
//     can grant is refill × hoursPerUnit (default 0.5 × 24h = 12h). Duration
//     stacks across launches, capped at gravOverchargeCapHours (default 72h /
//     3 days; slider runs to 7 days, then a top notch = ThanaticOverchargeCapUnlimited
//     analogue removes the cap). No psyfocus bump — that is thanatic's psychic
//     theme. See Hediff_GravOvercharge.
//     (Deferred: scaling refill / overcharge by gravship trip distance.)
public partial class ArchotechAndroidHardwareSettings
{
    // Grav reactor refill / overcharge (Odyssey-gated UI; the fields persist
    // regardless so a save toggling Odyssey doesn't lose its tuning).
    // gravRefillAmount default 0.5 = a launch tops up half a tank; overflow only
    // spills into Grav Overcharge when the reactor was already more than
    // (1 - refill) full at launch. See the class docblock + Hediff_GravOvercharge.
    public float gravRefillAmount = DefaultGravRefillAmount;
    public float gravOverchargeHoursPerUnit = DefaultGravOverchargeHoursPerUnit;
    public float gravOverchargeCapHours = DefaultGravOverchargeCapHours;
    // Sentinel mirroring ThanaticOverchargeCapUnlimited: the grav cap slider's
    // top notch (one hour past the 168h / 7-day meaningful max) removes the cap
    // entirely. Hediff_GravOvercharge treats any cap >= this as "no cap".
    public const float GravOverchargeCapUnlimited = 169f;

    private const float DefaultGravRefillAmount = 0.5f;
    private const float DefaultGravOverchargeHoursPerUnit = 24f;
    private const float DefaultGravOverchargeCapHours = 72f;

    private void ExposeGravReactorSettings()
    {
        Scribe_Values.Look(ref gravRefillAmount, "gravRefillAmount", DefaultGravRefillAmount);
        Scribe_Values.Look(ref gravOverchargeHoursPerUnit, "gravOverchargeHoursPerUnit", DefaultGravOverchargeHoursPerUnit);
        Scribe_Values.Look(ref gravOverchargeCapHours, "gravOverchargeCapHours", DefaultGravOverchargeCapHours);
    }

    private void ResetGravReactorSettings()
    {
        gravRefillAmount = DefaultGravRefillAmount;
        gravOverchargeHoursPerUnit = DefaultGravOverchargeHoursPerUnit;
        gravOverchargeCapHours = DefaultGravOverchargeCapHours;
    }

    private void DrawGravReactorSection(Listing_Standard listing)
    {
        // The grav reactor, its refill, and its overcharge are all driven by the
        // gravship launch mechanic, so the whole section is hidden without Odyssey.
        if (!ModsConfig.OdysseyActive)
            return;

        SectionHeader(listing, "AAH_SectionGravReactor".Translate());

        gravRefillAmount = SliderRow(listing,
            "AAH_GravRefill", "AAH_GravRefillDesc",
            gravRefillAmount, DefaultGravRefillAmount,
            0.05f, 1.0f, "F2", 0.01f);

        listing.Gap(10f);
        gravOverchargeHoursPerUnit = SliderRow(listing,
            "AAH_GravOverchargeHours", "AAH_GravOverchargeHoursDesc",
            gravOverchargeHoursPerUnit, DefaultGravOverchargeHoursPerUnit,
            1f, 48f, "F1", 0.1f);

        listing.Gap(10f);
        bool gravCapUnlimited = gravOverchargeCapHours >= GravOverchargeCapUnlimited;
        // Label reads in hours up to 72h, then switches to days (units change
        // from hours to days after 72h); the top notch is "Unlimited".
        string gravCapLabel = gravCapUnlimited
            ? "AAH_GravOverchargeCapUnlimited".Translate()
            : gravOverchargeCapHours > 72f
                ? "AAH_GravOverchargeCapDays".Translate((gravOverchargeCapHours / 24f).ToString("F1"))
                : "AAH_GravOverchargeCapHours".Translate(gravOverchargeCapHours.ToString("F0"));
        if (!gravCapUnlimited && Mathf.Approximately(gravOverchargeCapHours, DefaultGravOverchargeCapHours))
            gravCapLabel += "AAH_DefaultSuffix".Translate();
        string gravCapTooltip = "AAH_GravOverchargeCapDesc".Translate(DefaultGravOverchargeCapHours.ToString("F0"));
        // Sentinel scheme matches the Thanatic cap: the notch past the 168h
        // (7-day) meaningful max (GravOverchargeCapUnlimited) removes the cap.
        // Slider keeps whole-hour granularity so only the label unit changes,
        // not the step size, and the top notch lands on the sentinel int
        // deterministically rather than a near-max float.
        gravOverchargeCapHours = SliderRow(listing, gravCapLabel, gravCapTooltip,
            gravOverchargeCapHours, 6f, GravOverchargeCapUnlimited, step: 1f);

        // TODO: Scale refill fraction and overcharge hours by gravship trip
        // distance (straight-line tiles between the controller's takeoff /
        // landing tiles). Needs a normalization curve + a "reference distance
        // for full effect" knob. Deferred.

        listing.Gap(30f);
    }
}
