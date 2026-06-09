using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>"Psychic Transceiver" settings section.</summary>
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
    public float transceiverSensitivityOffset = TransceiverSensitivityOffsetDefault;

    // Psychic transceiver reprogramming unlock: when on (default), an awakened
    // android with the AAH_PsychicTransceiver implant permanently accepts
    // reprogramming at VREA's behavior station (which it would otherwise refuse).
    // The reliable, prerequisite-gated counterpart to the random inspiration —
    // the implant opens the android to outside (archotech) influence. Off makes
    // the transceiver a pure psychic-sensitivity implant with no station effect.
    public bool enableTransceiverReprogramming = true;

    private const float TransceiverSensitivityOffsetDefault = 0.25f;

    private void ExposePsychicTransceiverSettings()
    {
        Scribe_Values.Look(ref transceiverSensitivityOffset, "transceiverSensitivityOffset", TransceiverSensitivityOffsetDefault);
        Scribe_Values.Look(ref enableTransceiverReprogramming, "enableTransceiverReprogramming", true);
    }

    private void ResetPsychicTransceiverSettings()
    {
        transceiverSensitivityOffset = TransceiverSensitivityOffsetDefault;
        enableTransceiverReprogramming = true;
    }

    /// <summary>
    /// Push <see cref="transceiverSensitivityOffset"/> into the implant hediff's
    /// PsychicSensitivity stat offset. The offset is a single global value, so
    /// mutating the shared <see cref="HediffStage"/> StatModifier in place is correct
    /// (and cheaper than a per-pawn override). Called once at startup and on every
    /// settings write, so a live game picks up changes on the next stat query.
    /// </summary>
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
        SectionHeader(listing, "Psychic Transceiver");

        listing.Label(SensitivityOffsetLabel("Psychic sensitivity offset", transceiverSensitivityOffset),
            tooltip: "Offset applied to Psychic Sensitivity after suppressing the default " +
                "Psychically Deaf android hardware, restoring net sensitivity to 100% + this value " +
                "(e.g. +0.25 = 125%)");
        transceiverSensitivityOffset = SnapSensitivityOffset(
            listing.Slider(transceiverSensitivityOffset, -1.0f, 2.0f));

        listing.Gap(18f);

        listing.CheckboxLabeled("Psychic transceiver re-enables reprogramming",
            ref enableTransceiverReprogramming,
            "When enabled, an awakened android with a psychic transceiver installed will accept " +
            "reprogramming at an android behavior station, which it would otherwise refuse: the " +
            "implant's psychic bridge leaves it open to outside influence. Reprogramming " +
            "an android this way leaves it with a lingering unease that fades over the following weeks.\n\n" +
            "When disabled, the transceiver is purely a psychic-sensitivity implant with no effect " +
            "on behavior station usability.");

        listing.Gap(30f);
    }

    // Snap to 0.05 increments so every notable notch — the four trait-degree
    // offsets (−1.0, −0.5, 0.4, 0.8) and the default — is exactly reachable while
    // still allowing fine adjustment.
    private static float SnapSensitivityOffset(float raw) => Mathf.Round(raw * 20f) / 20f;

    // Composes "<title>: <±value> (<annotations…>)". Tags the four vanilla
    // psychic-sensitivity trait degrees and the mod default at their offsets.
    private static string SensitivityOffsetLabel(string title, float value)
    {
        var tags = new List<string>();
        if (Mathf.Approximately(value, -1.0f)) tags.Add("psychically deaf");
        else if (Mathf.Approximately(value, -0.5f)) tags.Add("psychically dull");
        else if (Mathf.Approximately(value, 0f)) tags.Add("baseliner");
        else if (Mathf.Approximately(value, 0.4f)) tags.Add("psychically sensitive");
        else if (Mathf.Approximately(value, 0.8f)) tags.Add("psychically hypersensitive");
        if (Mathf.Approximately(value, TransceiverSensitivityOffsetDefault)) tags.Add("default");

        string sign = value > 0f ? "+" : "";
        string suffix = tags.Count > 0 ? $" ({string.Join(", ", tags)})" : "";
        return $"{title}: {sign}{value:F2}{suffix}";
    }
}
