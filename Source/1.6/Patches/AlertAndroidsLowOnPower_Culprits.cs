using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// VREA workaround: null-safe replacement for Alert_AndroidsLowOnPower.get_Culprits.
//
// Problem: VREA's getter calls CurLevelPercentage on the ReactorPower need without
// null-checking. Our hediff's disablesNeeds removes that need entirely, so VREA
// hits a NullReferenceException on every alert tick for androids with our reactor.
//
// Fix: This prefix replaces the entire getter with a null-safe version that skips
// pawns whose ReactorPower need is absent (disabled by our hediff or any other means).
// Uses the ReactorPower NeedDef as the android detection mechanism since only
// androids have this need.
//
// Lifecycle context: Active at runtime whenever an android has the vanometric
// reactor installed (the need is disabled for the hediff's entire lifetime).
//
// Removable if: VREA adds a null-check before accessing the ReactorPower need
// in their alert code.
[HarmonyPatch]
public static class AlertAndroidsLowOnPower_Culprits_Patch
{
    private static readonly List<Pawn> Result = new();

    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.Alert_AndroidsLowOnPower");
        return type != null ? AccessTools.PropertyGetter(type, "Culprits") : null;
    }

    static bool Prefix(ref List<Pawn> __result)
    {
        Result.Clear();
        var reactorPowerNeed = AAH_DefOf.VREA_ReactorPower;

        if (reactorPowerNeed != null)
        {
            foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoSuspended)
            {
                var need = pawn.needs?.TryGetNeed(reactorPowerNeed);
                if (need?.CurLevelPercentage < 0.2f)
                    Result.Add(pawn);
            }
        }

        __result = Result;
        return false;
    }
}
