using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// "Miscellaneous" settings section — currently the Self-Determination
// inspiration knobs.
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
    public bool enableSelfDeterminationInspiration = DefaultEnableSelfDeterminationInspiration;
    public float selfDeterminationCommonalityWithPart = DefaultSelfDeterminationCommonalityWithPart;
    public float selfDeterminationCommonalityWithoutPart = DefaultSelfDeterminationCommonalityWithoutPart;

    private const bool DefaultEnableSelfDeterminationInspiration = true;
    private const float DefaultSelfDeterminationCommonalityWithPart = 3f;
    private const float RecommendedSelfDeterminationCommonalityWithPart = 3f;
    private const float DefaultSelfDeterminationCommonalityWithoutPart = 0f;
    private const float RecommendedSelfDeterminationCommonalityWithoutPart = 1f;

    private void ExposeSelfDeterminationSettings()
    {
        Scribe_Values.Look(ref enableSelfDeterminationInspiration, "enableSelfDeterminationInspiration", DefaultEnableSelfDeterminationInspiration);
        Scribe_Values.Look(ref selfDeterminationCommonalityWithPart, "selfDeterminationCommonalityWithPart", DefaultSelfDeterminationCommonalityWithPart);
        Scribe_Values.Look(ref selfDeterminationCommonalityWithoutPart, "selfDeterminationCommonalityWithoutPart", DefaultSelfDeterminationCommonalityWithoutPart);
    }

    private void ResetSelfDeterminationSettings()
    {
        enableSelfDeterminationInspiration = DefaultEnableSelfDeterminationInspiration;
        selfDeterminationCommonalityWithPart = DefaultSelfDeterminationCommonalityWithPart;
        selfDeterminationCommonalityWithoutPart = DefaultSelfDeterminationCommonalityWithoutPart;
    }

    private void DrawMiscellaneousSection(Listing_Standard listing)
    {
        SectionHeader(listing, "AAH_SectionMiscellaneous".Translate());

        listing.CheckboxLabeled("AAH_EnableSelfDetermination".Translate(),
            ref enableSelfDeterminationInspiration,
            "AAH_EnableSelfDeterminationDesc".Translate());
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

        selfDeterminationCommonalityWithPart = SliderRow(listing,
            CommonalitySliderLabel(selfDeterminationCommonalityWithPart,
                DefaultSelfDeterminationCommonalityWithPart, RecommendedSelfDeterminationCommonalityWithPart, withPart: true),
            "AAH_CommonalityWithPartDesc".Translate(),
            selfDeterminationCommonalityWithPart, 0f, 10f, step: 0.5f);

        listing.Gap(8f);

        selfDeterminationCommonalityWithoutPart = SliderRow(listing,
            CommonalitySliderLabel(selfDeterminationCommonalityWithoutPart,
                DefaultSelfDeterminationCommonalityWithoutPart, RecommendedSelfDeterminationCommonalityWithoutPart, withPart: false),
            "AAH_CommonalityWithoutPartDesc".Translate(),
            selfDeterminationCommonalityWithoutPart, 0f, 10f, step: 0.5f);

        listing.ColumnWidth += sliderIndent;
        listing.Outdent(sliderIndent);
        GUI.enabled = guiWasEnabled;

        listing.Gap(60f);
    }

    // Composes "<title>: <value><tags>" with inline parenthesized suffixes for
    // the value's notable meanings.
    private static string CommonalitySliderLabel(float value, float defaultValue, float recommendedValue, bool withPart)
    {
        var tags = new List<string>();
        if (value <= 0f) tags.Add("AAH_TagOff".Translate());
        if (Mathf.Approximately(value, 1f)) tags.Add("AAH_TagNoPassion".Translate());
        else if (Mathf.Approximately(value, 2.5f)) tags.Add("AAH_TagMinorPassion".Translate());
        else if (Mathf.Approximately(value, 5f)) tags.Add("AAH_TagMajorPassion".Translate());
        if (Mathf.Approximately(value, defaultValue)) tags.Add("AAH_TagDefault".Translate());
        // Skip "recommended" when it coincides with "default" to avoid redundancy.
        if (Mathf.Approximately(value, recommendedValue) && !Mathf.Approximately(recommendedValue, defaultValue))
            tags.Add("AAH_TagRecommended".Translate());

        // The "(a, b, c)" tag-list wrapper is list-formatting glue, not composed
        // English prose (see the matching note in Settings_PsychicTransceiver's
        // SensitivityOffsetLabel) — left as a literal; only the tag words
        // themselves are localized, via AAH_Tag* above.
        string suffix = tags.Count > 0 ? $" ({string.Join(", ", tags)})" : "";
        string titleKey = withPart ? "AAH_CommonalityWithPart" : "AAH_CommonalityWithoutPart";
        return titleKey.Translate(value.ToString("F1")) + suffix;
    }
}
