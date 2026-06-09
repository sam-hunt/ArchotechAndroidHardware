using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// "Grav Reactor" settings section (Odyssey only — the grav reactor, its refill,
/// and its overcharge are all driven by the gravship launch mechanic, so the
/// whole section is hidden without Odyssey; <see cref="DrawGravReactorSection"/>
/// early-returns).
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
public partial class ArchotechAndroidHardwareSettings
{
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

    private void ExposeGravReactorSettings()
    {
        Scribe_Values.Look(ref gravRefillAmount, "gravRefillAmount", 0.5f);
        Scribe_Values.Look(ref gravOverchargeHoursPerUnit, "gravOverchargeHoursPerUnit", 24f);
        Scribe_Values.Look(ref gravOverchargeCapHours, "gravOverchargeCapHours", 72f);
    }

    private void ResetGravReactorSettings()
    {
        gravRefillAmount = 0.5f;
        gravOverchargeHoursPerUnit = 24f;
        gravOverchargeCapHours = 72f;
    }

    private void DrawGravReactorSection(Listing_Standard listing)
    {
        // The grav reactor, its refill, and its overcharge are all driven by the
        // gravship launch mechanic, so the whole section is hidden without Odyssey.
        if (!ModsConfig.OdysseyActive)
            return;

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
}
