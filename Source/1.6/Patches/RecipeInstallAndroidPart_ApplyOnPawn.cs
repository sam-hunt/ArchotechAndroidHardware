using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// VREA workaround: fixes three issues in Recipe_InstallAndroidPart.ApplyOnPawn.
///
/// Problem 1 (item loss): VREA's ApplyOnPawn calls RestorePart(), which removes
/// all hediffs on the body part via RestorePartRecursiveInt. That internal method
/// destroys hediffs without checking spawnThingOnRemoved, so valuable items (like
/// our archotech reactor) are silently lost when a replacement part is installed.
///
/// Problem 2 (gene timing): When replacing our parts with another part, the
/// companion gene must be removed before VREA's code runs. Otherwise the overridden
/// gene stays suppressed during the replacement, and the new part's need/stat
/// setup may see incorrect state.
///
/// Problem 3 (stale cache): VREA's RestorePartRecursiveInt removes hediffs with
/// direct list manipulation (RemoveAt) followed by PostRemoved, but never calls
/// DirtyCache() on the hediff set. This leaves stale cached state, so
/// AddOrRemoveNeedsAsAppropriate (called during gene removal and PostRemoved)
/// may evaluate against an outdated hediff list.
///
/// Fix: The Prefix delegates ejection to <see cref="AAHPartEjector"/> (which
/// dispatches to <see cref="ICustomAAHEjection"/> for state-preserving hediffs
/// like Thanatic Reactor, or falls through to spawnThingOnRemoved otherwise)
/// and removes companion genes early for AAH_-prefixed hediffs. The Postfix
/// cleans up any orphaned genes (safety net for edge cases) and forces a final
/// needs recalculation after all hediff and gene changes have settled.
///
/// Lifecycle context: Active during part replacement surgery (installation phase).
/// Only triggers when a new part is installed on the same body part slot.
///
/// Removable if: VREA's RestorePart spawns spawnThingOnRemoved items, and their
/// RestorePartRecursiveInt calls DirtyCache() after hediff removal.
/// </summary>
[HarmonyPatch]
public static class RecipeInstallAndroidPart_ApplyOnPawn_Patch
{
    private static bool _needsRecalcPending;

    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.Recipe_InstallAndroidPart");
        return type != null
            ? AccessTools.Method(type, "ApplyOnPawn",
                new[] { typeof(Pawn), typeof(BodyPartRecord), typeof(Pawn), typeof(List<Thing>), typeof(Bill) })
            : null;
    }

    [HarmonyPrefix]
    public static void Prefix(Pawn pawn, BodyPartRecord part)
    {
        _needsRecalcPending = false;

        if (pawn?.health?.hediffSet == null || part == null)
            return;

        // Problem 1 fix: Pre-eject spawnThingOnRemoved items for our hediffs before
        // VREA's RestorePart destroys them without spawning.
        bool hasOurHediff = false;

        for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
        {
            var hediff = pawn.health.hediffSet.hediffs[i];
            if (hediff.Part != part)
                continue;

            if (!hediff.def.defName.StartsWith("AAH_"))
                continue;

            hasOurHediff = true;

            AAHPartEjector.Eject(hediff, pawn);
        }

        if (!hasOurHediff)
            return;

        _needsRecalcPending = true;
        SurgeryState.SuppressBodyPartDestruction = true;

        // Problem 2 fix: Remove companion genes early so overridden genes reactivate
        // before VREA's RestorePart runs. Each AAH_ hediff has a companion gene with
        // the same defName. Removing them here ensures the replacement part's setup
        // sees correct gene/need state.
        if (pawn.genes == null)
            return;

        for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
        {
            var hediff = pawn.health.hediffSet.hediffs[i];
            if (hediff.Part != part || !hediff.def.defName.StartsWith("AAH_"))
                continue;

            var geneDef = DefDatabase<GeneDef>.GetNamed(hediff.def.defName, errorOnFail: false);
            if (geneDef == null) continue;

            // Paired-slot hediffs (e.g., Neutrosynthesizer on kidneys) may still have
            // a sibling instance on the other slot. Leave the gene in place for it.
            if (pawn.health.hediffSet.hediffs.Any(h => h != hediff && h.def == hediff.def))
                continue;

            var gene = pawn.genes.GenesListForReading.FirstOrDefault(g => g.def == geneDef);
            if (gene != null)
                pawn.genes.RemoveGene(gene);
        }
    }

    [HarmonyPostfix]
    public static void Postfix(Pawn pawn)
    {
        SurgeryState.SuppressBodyPartDestruction = false;

        if (pawn?.genes == null || pawn.health?.hediffSet == null)
            return;

        // Safety net: remove any orphaned AAH_ companion genes whose hediff was
        // removed (e.g., RestorePart bypassed PostRemoved). Each AAH_ hediff has
        // a companion gene with the same defName.
        var genes = pawn.genes.GenesListForReading;
        for (int i = genes.Count - 1; i >= 0; i--)
        {
            var gene = genes[i];
            if (!gene.def.defName.StartsWith("AAH_"))
                continue;

            var hediffDef = DefDatabase<HediffDef>.GetNamed(gene.def.defName, errorOnFail: false);
            if (hediffDef != null && !pawn.health.hediffSet.HasHediff(hediffDef))
                pawn.genes.RemoveGene(gene);
        }

        // Problem 3 fix: Force a final needs recalculation after all changes have
        // settled. VREA's RestorePartRecursiveInt uses RemoveAt + PostRemoved without
        // calling DirtyCache(), so earlier recalculations during gene removal and
        // PostRemoved may have evaluated against stale hediff cache state.
        if (_needsRecalcPending)
        {
            _needsRecalcPending = false;
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();
        }
    }
}
