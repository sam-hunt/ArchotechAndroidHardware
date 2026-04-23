using System.Reflection;
using HarmonyLib;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// VREA workaround: make our <c>AAH_Hardware</c>-category companion genes test
/// as android genes so VREA's display code treats them like its own hardware/
/// subroutine genes.
///
/// Problem: VREA gates several UI-side behaviours on <c>Utils.IsAndroidGene()</c>:
///   • <c>GeneDef.GetDescriptionFull</c> postfix relabels biostatMet from
///     "Metabolism" to "Power efficiency" in the gene's description.
///   • <c>GeneDef.SpecialDisplayStats</c> postfix does the same relabel in the
///     stats table.
///   • <c>GeneUIUtility.RecacheGenes</c> (VREA's override) uses
///     <c>IsHardware()/IsSubroutine()</c> — both of which require
///     <c>IsAndroidGene()</c> — to decide which genes belong in the android
///     gene inspector's hardware/subroutine sections, and which genes'
///     biostatMet to sum into the displayed "Power efficiency" total.
/// <c>IsAndroidGene()</c> is a membership check against
/// <c>Utils.allAndroidGenes</c>, which VREA populates only for genes whose
/// <c>displayCategory</c> is <c>VREA_Hardware</c> or <c>VREA_Subroutine</c>.
/// Our companion genes live in <c>AAH_Hardware</c> so they fail the test —
/// biostatMet renders as "Metabolism" and is absent from the "Power
/// efficiency" total, even though our hediff's drain math reads it correctly.
///
/// Fix: Postfix <c>Utils.IsAndroidGene</c> to also return true for any
/// <c>GeneDef</c> whose <c>displayCategory</c> is <c>AAH_Hardware</c>. This
/// flips on all three display behaviours for every current and future AAH
/// hardware gene without per-gene patching.
///
/// Deliberate non-goal: we do NOT add our genes to <c>Utils.allAndroidGenes</c>.
/// That collection also feeds <c>AndroidGenesGenesInOrder</c>, which is what
/// the android creation dialog and behaviorist "modify android" dialog iterate
/// — adding our companion genes there would let players select reactor/implant
/// companion genes directly, which is wrong because they're lifecycle-managed
/// by their hediffs. Patching <c>IsAndroidGene</c> alone is narrower and
/// sidesteps that collection entirely.
///
/// Side effect: VREA's <c>RecacheGenes</c> now adds AAH_Hardware genes to the
/// inspector xenogenes list natively (via <c>IsHardware()</c>). The previous
/// <c>GeneUIUtility_RecacheGenes_Patch</c> is removed for this reason — keeping
/// both would double-insert each gene.
///
/// Removable if: VREA exposes an API to register display-only android genes,
/// or migrates its label/total logic to key off <c>displayCategory</c> directly
/// instead of <c>allAndroidGenes</c> membership.
/// </summary>
[HarmonyPatch]
public static class Utils_IsAndroidGene_Patch
{
    private static GeneCategoryDef _aahHardwareCategory;
    private static bool _aahHardwareCategoryResolved;

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

        if (!_aahHardwareCategoryResolved)
        {
            _aahHardwareCategory = DefDatabase<GeneCategoryDef>.GetNamed("AAH_Hardware", errorOnFail: false);
            _aahHardwareCategoryResolved = true;
        }
        if (_aahHardwareCategory == null) return;

        if (geneDef.displayCategory == _aahHardwareCategory)
            __result = true;
    }
}
