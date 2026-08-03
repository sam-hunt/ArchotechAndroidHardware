using System.Reflection;
using HarmonyLib;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// VREA workaround: make our AAH_Hardware-category companion genes test
// as android genes so VREA's display code treats them like its own hardware/
// subroutine genes.
//
// Problem: VREA gates several UI-side behaviours on Utils.IsAndroidGene():
//   • GeneDef.GetDescriptionFull postfix relabels biostatMet from
//     "Metabolism" to "Power efficiency" in the gene's description.
//   • GeneDef.SpecialDisplayStats postfix does the same relabel in the
//     stats table.
//   • GeneUIUtility.RecacheGenes (VREA's override) uses
//     IsHardware()/IsSubroutine() — both of which require
//     IsAndroidGene() — to decide which genes belong in the android
//     gene inspector's hardware/subroutine sections, and which genes'
//     biostatMet to sum into the displayed "Power efficiency" total.
// IsAndroidGene() is a membership check against
// Utils.allAndroidGenes, which VREA populates only for genes whose
// displayCategory is VREA_Hardware or VREA_Subroutine.
// Our companion genes live in AAH_Hardware so they fail the test —
// biostatMet renders as "Metabolism" and is absent from the "Power
// efficiency" total, even though our hediff's drain math reads it correctly.
//
// Fix: Postfix Utils.IsAndroidGene to also return true for any
// GeneDef whose displayCategory is AAH_Hardware. This
// flips on all three display behaviours for every current and future AAH
// hardware gene without per-gene patching.
//
// Deliberate non-goal: we do NOT add our genes to Utils.allAndroidGenes.
// That collection also feeds AndroidGenesGenesInOrder, which is what
// the android creation dialog and behaviorist "modify android" dialog iterate
// — adding our companion genes there would let players select reactor/implant
// companion genes directly, which is wrong because they're lifecycle-managed
// by their hediffs. Patching IsAndroidGene alone is narrower and
// sidesteps that collection entirely.
//
// Side effect: VREA's RecacheGenes now adds AAH_Hardware genes to the
// inspector xenogenes list natively (via IsHardware()). The previous
// GeneUIUtility_RecacheGenes_Patch is removed for this reason — keeping
// both would double-insert each gene.
//
// Removable if: VREA exposes an API to register display-only android genes,
// or migrates its label/total logic to key off displayCategory directly
// instead of allAndroidGenes membership.
[HarmonyPatch]
public static class Utils_IsAndroidGene_Patch
{
    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.Utils");
        return type != null ? AccessTools.Method(type, "IsAndroidGene") : null;
    }

    [HarmonyPostfix]
    public static void Postfix(GeneDef geneDef, ref bool __result)
    {
        if (__result) return;
        if (geneDef == null) return;

        var hardware = AAH_DefOf.AAH_Hardware;
        if (hardware == null) return;

        if (geneDef.displayCategory == hardware)
            __result = true;
    }
}
