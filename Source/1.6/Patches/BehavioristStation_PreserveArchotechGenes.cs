using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// Stops VREA's behaviorist station from duplicating our companion hardware genes
/// when an android's subroutines are reprogrammed.
///
/// <c>Building_AndroidBehavioristStation.FinishAndroidProject</c> applies the new
/// loadout by removing every gene in <c>Utils.allAndroidGenes</c> and then re-adding
/// the project's gene list. Our companion genes live in the <c>AAH_Hardware</c>
/// category, so they are <i>not</i> in <c>allAndroidGenes</c> and never get removed —
/// but the modify dialog seeds them into the project's gene list (they test as
/// android genes via <see cref="Utils_IsAndroidGene_Patch"/>). Re-adding a gene that
/// is still present would duplicate it: <c>Pawn_GeneTracker.AddGene(def, xenogene)</c>
/// does a bare <c>xenogenes.Add</c> with no dedup for xenogenes.
///
/// Fix: a Prefix that strips <c>AAH_Hardware</c> genes from <c>curAndroidProject.genes</c>
/// before the method runs. They are neither removed (not in <c>allAndroidGenes</c>)
/// nor re-added — the single existing copy persists untouched, with zero gene churn,
/// and its override re-resolves automatically as VREA re-adds the subroutine genes.
/// This also keeps the hediff as the sole authority over the companion gene's
/// lifecycle: VREA's reprogramming never touches it.
///
/// Coexists with <see cref="BehavioristStation_ConsumeSelfDetermination_Patch"/> as a
/// second, independent Prefix on the same method. Reflection-only: silently skipped
/// when VREA is absent. No-op for a normal loadout with no AAH genes.
/// </summary>
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
