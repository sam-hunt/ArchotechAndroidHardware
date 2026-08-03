using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Eligibility/commonality gate for the AAH_SelfDetermination inspiration.
//
// The inspiration is granted only to an awakened android, with a separate
// random-pool weight for those carrying one of this mod's parts (any
// AAH_Hardware-category companion gene) vs. those that don't — either
// weight at 0 makes that group ineligible (the two commonality settings; both at
// 0 disables the feature). Awakened is required because a non-awakened android
// can already use VREA's behavior station normally, so the inspiration would be
// pointless there.
//
// VREA's IsAwakened returns true for any pawn with no
// removeWhenAwakened android gene — including non-androids — so the
// explicit SelfDeterminationUtility.IsAndroid check is load-bearing
// whenever the without-part weight is above zero (else a plain human colonist
// would qualify).
public class InspirationWorker_SelfDetermination : InspirationWorker
{
    public override bool InspirationCanOccur(Pawn pawn)
    {
        if (ArchotechAndroidHardwareMod.Settings == null) return false;
        if (!base.InspirationCanOccur(pawn)) return false;

        // Android-only, awakened-only.
        if (!SelfDeterminationUtility.IsAndroid(pawn)) return false;
        if (!SelfDeterminationUtility.IsAwakened(pawn)) return false;

        // Eligible only if the applicable commonality is above zero (0 disables
        // that group; the master toggle off or both sliders at 0 disables it).
        return CommonalityFor(pawn) > 0f;
    }

    public override float CommonalityFor(Pawn pawn)
    {
        var settings = ArchotechAndroidHardwareMod.Settings;
        if (settings == null) return def.baseCommonality;
        if (!settings.enableSelfDeterminationInspiration) return 0f;

        float weight = HasAahPart(pawn)
            ? settings.selfDeterminationCommonalityWithPart
            : settings.selfDeterminationCommonalityWithoutPart;
        return Mathf.Max(0f, weight);
    }

    private static bool HasAahPart(Pawn pawn)
    {
        if (pawn?.genes == null) return false;

        var hardware = AAH_DefOf.AAH_Hardware;
        if (hardware == null) return false;

        return pawn.genes.GenesListForReading.Any(g => g.def.displayCategory == hardware);
    }
}
