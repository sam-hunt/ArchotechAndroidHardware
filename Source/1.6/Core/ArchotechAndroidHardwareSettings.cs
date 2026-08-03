using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Mod settings for the reactor balance knobs and render options.
//
// This class is split across several files (see Core/Settings/): each UI
// section owns its own fields, scribe entries, defaults, and draw method in a
// dedicated partial-class file, so adding or tuning a setting is a one-file
// edit. This file holds only the structural glue — the scroll/button frame in
// DoWindowContents, the per-section orchestration of ExposeData /
// ResetToDefaults, and the shared SectionHeader / CheckboxLabeled / SliderRow
// helpers.
//
// Shared balance reference (VREA 1.6, verified at implementation time —
// re-verify if VREA internals change). Both this mod's reactors take their
// drain rate from VREA's native biostatMet → curve path; no drain multiplier
// is applied in C#. Each reactor's section file documents its own specifics.
//
//   Baseline VREA reactor drain (Hediff_AndroidReactor.TickInterval):
//     per-check drain = 1.388889e-7 × PowerEfficiencyDrainMultiplier × 60
//                     ≈ 8.333e-6 energy per 60-tick interval
//     per-day drain (met=0) ≈ 0.008333 → full→empty in ~120 days
//
//   PowerEfficiencyToPowerDrainFactorCurve (AndroidStatsTable) — anchors we've
//   observed in VREA 1.6:
//     biostatMet sum -20 → 6.0× drain
//     biostatMet sum   0 → 1.0× drain (baseline)
//     biostatMet sum  +5 → 0.5× drain
//   The curve shape between anchors is VREA's, not ours, so intermediate
//   multipliers shouldn't be asserted as specific numbers here. The Thanatic
//   gene contributes biostatMet -4 (faster drain); the Grav gene +4 (slower).
public partial class ArchotechAndroidHardwareSettings : ModSettings
{
    // Transient UI state for the scrollable settings panel — not serialized.
    private Vector2 settingsScroll;
    private float settingsHeight;

    // Each section's fields, scribe entries, defaults, and draw method live in
    // its own partial-class file under Core/Settings/. These orchestrators just
    // fan out to them in display order; serialization order is immaterial
    // (Scribe is keyed by name).
    public override void ExposeData()
    {
        base.ExposeData();
        ExposeReactorSettings();
        ExposeThanaticReactorSettings();
        ExposeGravReactorSettings();
        ExposePsychicTransceiverSettings();
        ExposeSelfDeterminationSettings();
    }

    public void ResetToDefaults()
    {
        ResetReactorSettings();
        ResetThanaticReactorSettings();
        ResetGravReactorSettings();
        ResetPsychicTransceiverSettings();
        ResetSelfDeterminationSettings();
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

        DrawReactorsSection(listing);
        DrawThanaticReactorSection(listing);
        DrawGravReactorSection(listing);          // self-gated on Odyssey
        DrawPsychicTransceiverSection(listing);
        DrawMiscellaneousSection(listing);

        Text.Font = prevFont;
        settingsHeight = listing.CurHeight;
        listing.End();
        Widgets.EndScrollView();

        if (Widgets.ButtonText(buttonRect, "AAH_ResetToDefaults".Translate()))
            ResetToDefaults();
    }

    // Top-level section heading (medium font), e.g. "Reactors".
    private static void SectionHeader(Listing_Standard listing, string label)
    {
        Text.Font = GameFont.Medium;
        listing.Label(label);
        Text.Font = GameFont.Small;
        listing.Gap(10f);
    }

    // A Listing_Standard.CheckboxLabeled with a `disabled` flag the vanilla
    // listing helper lacks (only Widgets.CheckboxLabeled exposes one). When
    // disabled the row is dimmed and ignores clicks, so a dependent setting
    // reads as inert until its prerequisite is enabled — its stored value is
    // preserved, not forced. Mirrors the vanilla listing helper's
    // rect/tooltip/spacing so it lines up with the other rows.
    private static void CheckboxLabeled(Listing_Standard listing, string label, ref bool checkOn,
        string tooltip, bool disabled)
    {
        float height = Text.CalcHeight(label, listing.ColumnWidth);
        Rect rect = listing.GetRect(height);
        rect.width = Mathf.Min(rect.width + 24f, listing.ColumnWidth);
        if (!tooltip.NullOrEmpty())
        {
            if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, tooltip);
        }
        Color prev = GUI.color;
        // Widgets.CheckboxLabeled greys only the checkbox glyph when disabled, not
        // the label; dim the whole row so it reads uniformly inactive.
        if (disabled) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.5f);
        Widgets.CheckboxLabeled(rect, label, ref checkOn, disabled);
        GUI.color = prev;
        listing.Gap(listing.verticalSpacing);
    }

    // House-style numeric slider row: draws a pre-composed, value-bearing label
    // (already carrying any bespoke annotations, e.g. trait-degree tags) with a
    // hover tooltip, then a slider snapped to `step` (measured from `min` so the
    // snap grid is stable regardless of where the range starts). Returns the
    // snapped value. This is the low-level draw call every section funnels
    // through; sections with bespoke label composition (tag lists, unit
    // switching) build the label themselves and call this overload directly.
    private static float SliderRow(Listing_Standard listing, string label, string tooltip,
        float value, float min, float max, float step = 1f)
    {
        listing.Label(label, tooltip: tooltip);
        return Mathf.Round((listing.Slider(value, min, max) - min) / step) * step + min;
    }

    // Convenience overload for the common case: a translated "<title>: <value>"
    // label/tooltip pair (Keyed strings taking the value as {0}), with a
    // trailing "(default)" tag appended once the value is at its shipped
    // default (Mathf.Approximately, not ==, since step-snapping doesn't always
    // reproduce the exact default float).
    private static float SliderRow(Listing_Standard listing, string labelKey, string tooltipKey,
        float value, float defaultValue, float min, float max, string format = "F2", float step = 0.01f)
    {
        string label = labelKey.Translate(value.ToString(format));
        if (Mathf.Approximately(value, defaultValue))
            label += "AAH_DefaultSuffix".Translate();
        string tooltip = tooltipKey.Translate(defaultValue.ToString(format));
        return SliderRow(listing, label, tooltip, value, min, max, step);
    }
}
