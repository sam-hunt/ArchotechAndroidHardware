using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// VREA workaround: injects our gene into the Biotech gene inspector display.
///
/// Problem: VREA replaces the entire Biotech gene inspector tab for androids
/// with a custom UI. This custom UI only renders genes found in VREA's private
/// allAndroidGenes HashSet, which is populated from VREA's own AndroidGeneDefs.
/// Our companion genes (standard GeneDefs, not AndroidGeneDefs) are invisible in
/// this UI. We cannot add to allAndroidGenes because it also drives the android
/// creation dialog and behaviorist station -- adding a non-AndroidGeneDef would
/// break those.
///
/// Fix: This postfix runs after VREA's RecacheGenes populates RimWorld's
/// GeneUIUtility.xenogenes static list. We append any AAH_-prefixed genes to
/// that list so they appear in the inspector alongside VREA's android genes.
/// This is display-only and does not affect gene logic or creation dialogs.
///
/// Lifecycle context: Active whenever the gene inspector panel is rendered for
/// a pawn that has any of our companion genes.
///
/// Removable if: VREA renders all genes present on the pawn (not just those in
/// allAndroidGenes), or provides an API to register additional display-only genes.
/// </summary>
[HarmonyPatch]
public static class GeneUIUtility_RecacheGenes_Patch
{
    private static List<Gene> _xenogenesList;

    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.GeneUIUtility_DrawGenesInfo_Patch");
        return type != null ? AccessTools.Method(type, "RecacheGenes") : null;
    }

    [HarmonyPostfix]
    public static void Postfix(Thing target)
    {
        if (target is not Pawn pawn || pawn.genes == null) return;

        _xenogenesList ??= (List<Gene>)AccessTools.Field(typeof(GeneUIUtility), "xenogenes").GetValue(null);

        var genes = pawn.genes.GenesListForReading;
        for (int i = 0; i < genes.Count; i++)
        {
            if (genes[i].def.defName.StartsWith("AAH_"))
                _xenogenesList.Add(genes[i]);
        }
    }
}
