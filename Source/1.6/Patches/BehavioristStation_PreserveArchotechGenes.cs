using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace ArchotechAndroidHardware.VREAPatches;

// Stops VREA's behaviorist station from duplicating our companion hardware genes
// when an android's subroutines are reprogrammed.
//
// Building_AndroidBehavioristStation.FinishAndroidProject applies the new
// loadout by removing every gene in Utils.allAndroidGenes and then re-adding
// the project's gene list. Our companion genes live in the AAH_Hardware
// category, so they are not in allAndroidGenes and never get removed —
// but the modify dialog seeds them into the project's gene list (they test as
// android genes via Utils_IsAndroidGene_Patch). Re-adding a gene that
// is still present would duplicate it: Pawn_GeneTracker.AddGene(def, xenogene)
// does a bare xenogenes.Add with no dedup for xenogenes.
//
// Fix: a Prefix that strips AAH_Hardware genes from curAndroidProject.genes
// before the method runs. They are neither removed (not in allAndroidGenes)
// nor re-added — the single existing copy persists untouched, with zero gene churn,
// and its override re-resolves automatically as VREA re-adds the subroutine genes.
// This also keeps the hediff as the sole authority over the companion gene's
// lifecycle: VREA's reprogramming never touches it.
//
// Coexists with BehavioristStation_ConsumeSelfDetermination_Patch as a
// second, independent Prefix on the same method. Reflection-only: silently skipped
// when VREA is absent. No-op for a normal loadout with no AAH genes.
[HarmonyPatch]
public static class BehavioristStation_PreserveArchotechGenes_Patch
{
    private static FieldInfo _projectField;
    private static bool _projectFieldResolved;

    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.Building_AndroidBehavioristStation");
        return type != null ? AccessTools.Method(type, "FinishAndroidProject") : null;
    }

    [HarmonyPrefix]
    public static void Prefix(object __instance)
    {
        var hardware = AAH_DefOf.AAH_Hardware;
        if (hardware == null) return;

        var project = GetProject(__instance);
        project?.genes?.RemoveAll(g => g.displayCategory == hardware);
    }

    private static CustomXenotype GetProject(object station)
    {
        if (station == null) return null;
        if (!_projectFieldResolved)
        {
            _projectField = AccessTools.Field(station.GetType(), "curAndroidProject");
            _projectFieldResolved = true;
        }
        return _projectField?.GetValue(station) as CustomXenotype;
    }
}
