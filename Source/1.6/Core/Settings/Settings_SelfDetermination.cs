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
    // enable — master toggle for granting the inspiration at all.
    // allowForAllAwakened — when off (default), only awakened androids carrying
    //   an AAH part are eligible (keeps the feature in our mod's lane); when on,
    //   any awakened colonist android can roll it.
    // commonality — random-pool weight; consumed by InspirationWorker.CommonalityFor.
    public bool enableSelfDeterminationInspiration = true;
    public bool allowSelfDeterminationForAllAwakened = false;
    public float selfDeterminationCommonality = 3f;

    private void ExposeSelfDeterminationSettings()
    {
        Scribe_Values.Look(ref enableSelfDeterminationInspiration, "enableSelfDeterminationInspiration", true);
        Scribe_Values.Look(ref allowSelfDeterminationForAllAwakened, "allowSelfDeterminationForAllAwakened", false);
        Scribe_Values.Look(ref selfDeterminationCommonality, "selfDeterminationCommonality", 3f);
    }

    private void ResetSelfDeterminationSettings()
    {
        enableSelfDeterminationInspiration = true;
        allowSelfDeterminationForAllAwakened = false;
        selfDeterminationCommonality = 3f;
    }

    private void DrawMiscellaneousSection(Listing_Standard listing)
    {
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
    }
}
