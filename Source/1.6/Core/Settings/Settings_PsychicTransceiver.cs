using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// "Psychic Transceiver" settings section.
public partial class ArchotechAndroidHardwareSettings
{
    // Psychic sensitivity offset granted by the implant. The companion gene first
    // removes VREA's ×0 "psychically deaf" factor (restoring the 100% StatDef base),
    // then this offset stacks on top — so net sensitivity is 100% + this value.
    // Pushed into the AAH_PsychicTransceiver hediff stage's
    // statOffset by ApplyTransceiverSensitivityOffset; the XML value is just the
    // load-time default we overwrite. Range −1.0…2.0; the slider tags the four
    // vanilla psychic-sensitivity trait degrees as reference points (an offset equal
    // to a degree's gives the same net sensitivity a human with that trait would
    // have — verified vs Core Traits_Spectrum.xml).
    public float transceiverSensitivityOffset = DefaultTransceiverSensitivityOffset;

    // Psychic transceiver reprogramming unlock: when on (default), an awakened
    // android with the AAH_PsychicTransceiver implant permanently accepts
    // reprogramming at VREA's behavior station (which it would otherwise refuse).
    // The reliable, prerequisite-gated counterpart to the random inspiration —
    // the implant opens the android to outside (archotech) influence. Off makes
    // the transceiver a pure psychic-sensitivity implant with no station effect.
    public bool enableTransceiverReprogramming = DefaultEnableTransceiverReprogramming;

    private const float DefaultTransceiverSensitivityOffset = 0.25f;
    private const bool DefaultEnableTransceiverReprogramming = true;

    private void ExposePsychicTransceiverSettings()
    {
        Scribe_Values.Look(ref transceiverSensitivityOffset, "transceiverSensitivityOffset", DefaultTransceiverSensitivityOffset);
        Scribe_Values.Look(ref enableTransceiverReprogramming, "enableTransceiverReprogramming", DefaultEnableTransceiverReprogramming);
    }

    private void ResetPsychicTransceiverSettings()
    {
        transceiverSensitivityOffset = DefaultTransceiverSensitivityOffset;
        enableTransceiverReprogramming = DefaultEnableTransceiverReprogramming;
    }

    // Push transceiverSensitivityOffset into the implant hediff's
    // PsychicSensitivity stat offset. The offset is a single global value, so
    // mutating the shared HediffStage StatModifier in place is correct (and
    // cheaper than a per-pawn override). Called once at startup and on every
    // settings write, so a live game picks up changes on the next stat query.
    public void ApplyTransceiverSensitivityOffset()
    {
        HediffStage stage = AAH_HediffDefOf.AAH_PsychicTransceiver?.stages?.FirstOrDefault();
        if (stage == null) return;

        stage.statOffsets ??= new List<StatModifier>();
        StatModifier mod = stage.statOffsets.FirstOrDefault(s => s.stat == StatDefOf.PsychicSensitivity);
        if (mod == null)
        {
            mod = new StatModifier { stat = StatDefOf.PsychicSensitivity };
            stage.statOffsets.Add(mod);
        }
        mod.value = transceiverSensitivityOffset;
    }

    private void DrawPsychicTransceiverSection(Listing_Standard listing)
    {
        SectionHeader(listing, "AAH_SectionPsychicTransceiver".Translate());

        // Step 0.05 so every notable notch — the four trait-degree offsets
        // (−1.0, −0.5, 0.4, 0.8) and the default — is exactly reachable while
        // still allowing fine adjustment.
        transceiverSensitivityOffset = SliderRow(listing, SensitivityOffsetLabel(transceiverSensitivityOffset),
            "AAH_TransceiverSensitivityDesc".Translate(),
            transceiverSensitivityOffset, -1.0f, 2.0f, step: 0.05f);

        listing.Gap(18f);

        listing.CheckboxLabeled("AAH_TransceiverReprogramming".Translate(),
            ref enableTransceiverReprogramming,
            "AAH_TransceiverReprogrammingDesc".Translate());

        listing.Gap(30f);
    }

    // Composes "<title>: <±value><tags>". Tags the four vanilla
    // psychic-sensitivity trait degrees and the mod default at their offsets.
    private static string SensitivityOffsetLabel(float value)
    {
        var tags = new List<string>();
        if (Mathf.Approximately(value, -1.0f)) tags.Add("AAH_TagPsychicallyDeaf".Translate());
        else if (Mathf.Approximately(value, -0.5f)) tags.Add("AAH_TagPsychicallyDull".Translate());
        else if (Mathf.Approximately(value, 0f)) tags.Add("AAH_TagBaseliner".Translate());
        else if (Mathf.Approximately(value, 0.4f)) tags.Add("AAH_TagPsychicallySensitive".Translate());
        else if (Mathf.Approximately(value, 0.8f)) tags.Add("AAH_TagPsychicallyHypersensitive".Translate());
        if (Mathf.Approximately(value, DefaultTransceiverSensitivityOffset)) tags.Add("AAH_TagDefault".Translate());

        // The leading "+" and the "(a, b, c)" tag-list wrapper are numeric/list
        // formatting glue, not composed English prose — mirrors vanilla's own
        // untranslated stat-offset sign and parenthetical trait/passion suffixes
        // (e.g. Traits_Spectrum tooltips), so they're left as literals rather
        // than routed through Keyed strings. Only the words inside (the tags
        // themselves) are localized, via AAH_Tag* above.
        string sign = value > 0f ? "+" : "";
        string suffix = tags.Count > 0 ? $" ({string.Join(", ", tags)})" : "";
        return "AAH_TransceiverSensitivity".Translate($"{sign}{value:F2}") + suffix;
    }
}
