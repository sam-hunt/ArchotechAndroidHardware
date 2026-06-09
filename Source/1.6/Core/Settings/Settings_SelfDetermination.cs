using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// "Miscellaneous" settings section — currently the Self-Determination
/// inspiration knobs.
/// </summary>
public partial class ArchotechAndroidHardwareSettings
{
    // Self-Determination inspiration: lets an awakened android voluntarily
    // reprogram its subroutines at VREA's behavior station for one session.
    //
    // A master enable toggle plus two commonality sliders (all consumed by
    // InspirationWorker_SelfDetermination). The random-pool weight is set
    // separately for awakened androids that carry an AAH part vs. those that
    // don't; a weight of 0 makes that group ineligible. While the toggle is off
    // the worker treats both weights as 0, but the slider values are preserved so
    // re-enabling restores them (rather than leaving both stuck at 0).
    //   WithPart    — default 3.0 / recommended 3.0 (our mod's core audience).
    //   WithoutPart — default 0.0 / recommended 1.0. 0 keeps parity with VREA
    //     balance (no inspiration for vanilla androids); 1.0 matches a plain
    //     vanilla inspiration weight if you want to broaden it.
    public bool enableSelfDeterminationInspiration = true;
    public float selfDeterminationCommonalityWithPart = WithPartDefault;
    public float selfDeterminationCommonalityWithoutPart = WithoutPartDefault;

    private const float WithPartDefault = 3f;
    private const float WithPartRecommended = 3f;
    private const float WithoutPartDefault = 0f;
    private const float WithoutPartRecommended = 1f;

    private void ExposeSelfDeterminationSettings()
    {
        Scribe_Values.Look(ref enableSelfDeterminationInspiration, "enableSelfDeterminationInspiration", true);
        Scribe_Values.Look(ref selfDeterminationCommonalityWithPart, "selfDeterminationCommonalityWithPart", WithPartDefault);
        Scribe_Values.Look(ref selfDeterminationCommonalityWithoutPart, "selfDeterminationCommonalityWithoutPart", WithoutPartDefault);
    }

    private void ResetSelfDeterminationSettings()
    {
        enableSelfDeterminationInspiration = true;
        selfDeterminationCommonalityWithPart = WithPartDefault;
        selfDeterminationCommonalityWithoutPart = WithoutPartDefault;
    }

    private void DrawMiscellaneousSection(Listing_Standard listing)
    {
        SectionHeader(listing, "Miscellaneous");

        listing.CheckboxLabeled("Enable Self-Determination inspiration",
            ref enableSelfDeterminationInspiration,
            "Awakened androids may occasionally gain a Self-Determination inspiration, " +
            "during which subroutine reprogramming at an android behavior station is accepted " +
            "(which it would otherwise refuse).");
        listing.Gap(12f);

        // Grey out + freeze the sliders while the inspiration is disabled; their
        // stored values are preserved (GUI.enabled = false suppresses interaction,
        // so Slider returns the value unchanged) and the worker treats them as 0.
        // Inline parenthesized annotations on each label (off / default /
        // recommended / vanilla-passion reference points) mirror the
        // UniqueWeaponsUnbound settings style; detail lives in the tooltip.
        bool guiWasEnabled = GUI.enabled;
        GUI.enabled = enableSelfDeterminationInspiration;

        // Indent the dependent sliders so they read as children of the toggle.
        // Shrink ColumnWidth by the same amount so the slider rects (sized off
        // ColumnWidth) stay within the right edge rather than overflowing; the
        // listing is rebuilt each frame, so restoring afterward is just tidiness.
        const float sliderIndent = 16f;
        listing.Indent(sliderIndent);
        listing.ColumnWidth -= sliderIndent;

        listing.Label(CommonalitySliderLabel("Commonality (AAH augmented)",
                selfDeterminationCommonalityWithPart, WithPartDefault, WithPartRecommended),
            tooltip: "How likely this inspiration is picked among eligible inspirations for awakened androids " +
                "with at least one Archotech Android Hardware body part installed.");
        selfDeterminationCommonalityWithPart = SnapCommonality(
            listing.Slider(selfDeterminationCommonalityWithPart, 0f, 10f));

        listing.Gap(8f);

        listing.Label(CommonalitySliderLabel("Commonality (VREA stock)",
                selfDeterminationCommonalityWithoutPart, WithoutPartDefault, WithoutPartRecommended),
            tooltip: "How likely this inspiration is picked among eligible inspirations for awakened androids " +
                "without any Archotech Android Hardware body parts installed.");
        selfDeterminationCommonalityWithoutPart = SnapCommonality(
            listing.Slider(selfDeterminationCommonalityWithoutPart, 0f, 10f));

        listing.ColumnWidth += sliderIndent;
        listing.Outdent(sliderIndent);
        GUI.enabled = guiWasEnabled;

        listing.Gap(60f);
    }

    // Snap to 0.5 increments; 0 disables that group.
    private static float SnapCommonality(float raw) => Mathf.Round(raw * 2f) / 2f;

    // Composes "<title>: <value> (<annotations…>)" with inline parenthesized
    // suffixes for the value's notable meanings.
    private static string CommonalitySliderLabel(string title, float value, float defaultValue, float recommendedValue)
    {
        var tags = new List<string>();
        if (value <= 0f) tags.Add("off");
        if (Mathf.Approximately(value, 1f)) tags.Add("no passion");
        else if (Mathf.Approximately(value, 2.5f)) tags.Add("minor passion");
        else if (Mathf.Approximately(value, 5f)) tags.Add("major passion");
        if (Mathf.Approximately(value, defaultValue)) tags.Add("default");
        // Skip "recommended" when it coincides with "default" to avoid redundancy.
        if (Mathf.Approximately(value, recommendedValue) && !Mathf.Approximately(recommendedValue, defaultValue))
            tags.Add("recommended");

        string suffix = tags.Count > 0 ? $" ({string.Join(", ", tags)})" : "";
        return $"{title}: {value:F1}{suffix}";
    }
}
