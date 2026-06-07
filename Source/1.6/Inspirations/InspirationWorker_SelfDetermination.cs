using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Eligibility/commonality gate for the <c>AAH_SelfDetermination</c> inspiration.
///
/// The inspiration is granted only to an <b>awakened android</b> that either
/// carries one of this mod's parts (any <c>AAH_Hardware</c>-category companion
/// gene) or — when the broaden setting is on — to any awakened android. Awakened
/// is required because a non-awakened android can already use VREA's behavior
/// station normally, so the inspiration would be pointless there.
///
/// VREA's <c>IsAwakened</c> returns true for any pawn with no
/// <c>removeWhenAwakened</c> android gene — including non-androids — so the
/// explicit <see cref="SelfDeterminationUtility.IsAndroid"/> check is load-bearing
/// for the broaden path (else a plain human colonist would qualify).
/// </summary>
public class InspirationWorker_SelfDetermination : InspirationWorker
{
    public override bool InspirationCanOccur(Pawn pawn)
    {
        var settings = ArchotechAndroidHardwareMod.Settings;
        if (settings == null || !settings.enableSelfDeterminationInspiration) return false;
        if (!base.InspirationCanOccur(pawn)) return false;

        // Android-only, awakened-only.
        if (!SelfDeterminationUtility.IsAndroid(pawn)) return false;
        if (!SelfDeterminationUtility.IsAwakened(pawn)) return false;

        // Default: must carry an AAH part. Broaden setting lifts that requirement.
        if (!settings.allowSelfDeterminationForAllAwakened && !HasAahPart(pawn)) return false;

        return true;
    }

    public override float CommonalityFor(Pawn pawn)
    {
        var settings = ArchotechAndroidHardwareMod.Settings;
        return settings != null ? Mathf.Max(0f, settings.selfDeterminationCommonality) : def.baseCommonality;
    }

    private static bool HasAahPart(Pawn pawn)
    {
        if (pawn?.genes == null) return false;

        var hardware = AAH_DefOf.AAH_Hardware;
        if (hardware == null) return false;

        return pawn.genes.GenesListForReading.Any(g => g.def.displayCategory == hardware);
    }
}
