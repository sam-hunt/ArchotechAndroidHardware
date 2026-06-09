using Verse;

namespace ArchotechAndroidHardware;

/// <summary>"Psychic Transceiver" settings section.</summary>
public partial class ArchotechAndroidHardwareSettings
{
    // Psychic transceiver reprogramming unlock: when on (default), an awakened
    // android with the AAH_PsychicTransceiver implant permanently accepts
    // reprogramming at VREA's behavior station (which it would otherwise refuse).
    // The reliable, prerequisite-gated counterpart to the random inspiration —
    // the implant opens the android to outside (archotech) influence. Off makes
    // the transceiver a pure psychic-sensitivity implant with no station effect.
    public bool enableTransceiverReprogramming = true;

    private void ExposePsychicTransceiverSettings()
    {
        Scribe_Values.Look(ref enableTransceiverReprogramming, "enableTransceiverReprogramming", true);
    }

    private void ResetPsychicTransceiverSettings()
    {
        enableTransceiverReprogramming = true;
    }

    private void DrawPsychicTransceiverSection(Listing_Standard listing)
    {
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
    }
}
