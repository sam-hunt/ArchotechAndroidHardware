using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// Transfers a Thanatic Reactor item's storedEnergy onto the installed
// Hediff_ThanaticReactor during install surgery. Without this,
// every installation would start the hediff at Energy = 1f (PostAdd default),
// wiping the ingredient item's stored charge and breaking reactor
// transferability between android pawns.
//
// Flow:
//   Prefix  — scan ingredients for a ThanaticReactorThing,
//             stash its storedEnergy into _pendingEnergy.
//   (original ApplyOnPawn runs; VREA's code adds the hediff at Energy = 1f)
//   Postfix — if we stashed energy AND the pawn now has a Thanatic Reactor
//             hediff, overwrite its Energy with the stashed value, then clear.
//
// Coexists with RecipeInstallAndroidPart_ApplyOnPawn_Patch on
// the same target method. The two patches don't interact — ejection/gene
// cleanup vs. energy transfer — and can run in either order.
[HarmonyPatch]
public static class ThanaticReactorInstallEnergyTransfer_Patch
{
    private static float? _pendingEnergy;
    private static HediffDef ThanaticReactorDef => AAH_HediffDefOf.AAH_ThanaticReactor;

    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.Recipe_InstallAndroidPart");
        return type != null
            ? AccessTools.Method(type, "ApplyOnPawn",
                new[] { typeof(Pawn), typeof(BodyPartRecord), typeof(Pawn), typeof(List<Thing>), typeof(Bill) })
            : null;
    }

    [HarmonyPrefix]
    public static void Prefix(List<Thing> ingredients)
    {
        _pendingEnergy = null;
        if (ingredients == null) return;

        foreach (var thing in ingredients)
        {
            // ThingMaker.MakeThing produces the concrete class declared via
            // ThingDef.thingClass, so a direct is-check is sufficient.
            if (thing is ThanaticReactorThing tr)
            {
                _pendingEnergy = tr.storedEnergy;
                return;
            }
        }
    }

    [HarmonyPostfix]
    public static void Postfix(Pawn pawn)
    {
        if (_pendingEnergy == null) return;
        var energy = _pendingEnergy.Value;
        _pendingEnergy = null;

        if (pawn?.health?.hediffSet == null) return;
        var def = ThanaticReactorDef;
        if (def == null) return;

        if (pawn.health.hediffSet.GetFirstHediffOfDef(def) is Hediff_ThanaticReactor hediff)
            hediff.Energy = energy;
    }
}
