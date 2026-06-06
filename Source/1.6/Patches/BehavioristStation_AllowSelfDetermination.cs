using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// Lets an awakened android with the <c>AAH_SelfDetermination</c> inspiration use
/// VREA's Android Behavior Station.
///
/// VREA's <c>Building_AndroidBehavioristStation.CanAcceptPawn(Pawn)</c> permanently
/// refuses awakened colonist androids:
/// <code>if (selPawn.IsAwakened() &amp;&amp; selPawn.IsColonist &amp;&amp; !selPawn.IsPrisoner)
///         return Translate("VREA.RefusesReprogramming");</code>
/// While the inspiration is active we flip that refusal to "accepted". Once inside,
/// no further patching is needed: the modification dialog's <c>GeneValidator</c>
/// already lets awakened androids toggle every non-<c>removeWhenAwakened</c> gene
/// (which is all subroutines), and re-selecting subroutines doesn't un-awaken the
/// pawn (awakened == "no removeWhenAwakened gene present"; no subroutine is one).
///
/// We only override the awakening refusal — never the no-power / quest-lodger
/// refusals — by confirming the pawn is awakened and then re-validating the gates
/// that VREA checks <i>after</i> the awakening line (those never ran, because the
/// awakening check returned first).
///
/// Reflection-only: target resolved via <c>AccessTools.TypeByName</c>; the patch is
/// silently skipped if VREA is absent.
/// </summary>
[HarmonyPatch]
public static class BehavioristStation_AllowSelfDetermination_Patch
{
    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.Building_AndroidBehavioristStation");
        return type != null ? AccessTools.Method(type, "CanAcceptPawn", new[] { typeof(Pawn) }) : null;
    }

    [HarmonyPostfix]
    public static void Postfix(object __instance, Pawn selPawn, ref AcceptanceReport __result)
    {
        if (__result.Accepted) return;                              // already allowed
        if (!SelfDeterminationUtility.IsActiveOn(selPawn)) return;  // no inspiration → keep refusal
        if (!SelfDeterminationUtility.IsAwakened(selPawn)) return;  // only the awakening refusal is ours to override

        // Re-validate the gates VREA checks after the awakening line.
        if (QuestUtility.IsQuestLodger(selPawn)) return;
        var power = (__instance as Thing)?.TryGetComp<CompPowerTrader>();
        if (power != null && !power.PowerOn) return;

        __result = true;
    }
}
