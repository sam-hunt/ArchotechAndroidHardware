using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// Lets an awakened android use VREA's Android Behavior Station when one of two
// triggers is present: the temporary AAH_SelfDetermination inspiration (the
// android's own will) or a permanently installed psychic transceiver (the android
// opened to outside/archotech influence — see
// SelfDeterminationUtility.HasReprogrammingImplant).
//
// VREA's Building_AndroidBehavioristStation.CanAcceptPawn(Pawn) permanently
// refuses awakened colonist androids:
// if (selPawn.IsAwakened() && selPawn.IsColonist && !selPawn.IsPrisoner)
//         return Translate("VREA.RefusesReprogramming");
// While either trigger holds we flip that refusal to "accepted". Once inside,
// no further patching is needed: the modification dialog's GeneValidator
// already lets awakened androids toggle every non-removeWhenAwakened gene
// (which is all subroutines), and re-selecting subroutines doesn't un-awaken the
// pawn (awakened == "no removeWhenAwakened gene present"; no subroutine is one).
//
// We only override the awakening refusal — never the no-power / quest-lodger
// refusals — by confirming the pawn is awakened and then re-validating the gates
// that VREA checks after the awakening line (those never ran, because the
// awakening check returned first).
//
// Reflection-only: target resolved via AccessTools.TypeByName; the patch is
// silently skipped if VREA is absent.
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

        // Either trigger lifts the awakening refusal: the active inspiration or the implant.
        bool inspired = SelfDeterminationUtility.IsActiveOn(selPawn);
        bool implanted = SelfDeterminationUtility.HasReprogrammingImplant(selPawn);
        if (!inspired && !implanted) return;                        // neither → keep refusal
        if (!SelfDeterminationUtility.IsAwakened(selPawn)) return;  // only the awakening refusal is ours to override

        // Re-validate the gates VREA checks after the awakening line.
        if (QuestUtility.IsQuestLodger(selPawn)) return;
        var power = (__instance as Thing)?.TryGetComp<CompPowerTrader>();
        if (power?.PowerOn == false) return;

        __result = true;
    }
}
