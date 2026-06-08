using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// Lets VREA's android creation / behaviorist-modify dialog confirm a loadout that
/// contains one of our companion hardware genes, without weakening its genuine
/// "two conflicting subroutines selected together" guard.
///
/// Problem: our companion genes share an exclusion tag with the VREA gene they
/// override (that overlap is what drives Biotech's runtime suppression). The modify
/// dialog seeds <c>selectedGenes</c> from the pawn's live genes — and because the
/// <see cref="Utils_IsAndroidGene_Patch"/> makes our genes test as android genes,
/// <i>both</i> our gene and the VREA gene it overrides land in that set. Vanilla
/// <c>GeneCreationDialogBase.OnGenesChanged</c> then groups the tag-sharing pair into
/// a <see cref="GeneLeftChosenGroup"/> (ours wins, the VREA gene is "suppressed"), and
/// VREA's <c>Window_CreateAndroidBase.CanAccept</c> / conflict footer hard-reject on
/// <c>leftChosenGroups.Any()</c>. So the intended, permanent override reads as a
/// blocking conflict and the dialog can't be confirmed.
///
/// Fix (borrow-and-restore around <c>Window_CreateAndroidBase.DoBottomButtons</c>):
/// just before the Accept gate + footer run, drop from <c>leftChosenGroups</c> the
/// groups that exist <i>only</i> because of our override; restore them immediately
/// after. The order within one frame is DrawGenes (line ~105) → DoBottomButtons
/// (line ~206), so the per-gene "suppressed" dimming — drawn earlier from the full
/// list — is untouched: the VREA gene still visibly reads as overridden, which is the
/// clearest signal of how our hardware interacts with VREA's. <c>met</c> (power
/// efficiency) is computed separately in <c>OnGenesChanged</c> from
/// <c>NonOverriddenGenes()</c>, so it is unaffected and keeps showing the real,
/// override-active value.
///
/// Precise, not coarse: a group is dropped only when it is led by one of our genes
/// (always the case — our <c>AAH_Hardware</c> category sits at priority 10000, so our
/// gene is always the leftmost/active member) AND its overridden members carry no
/// genuine conflict <i>among themselves</i>. A real VREA conflict therefore still
/// blocks even if our gene is part of the merged group — e.g. the mnemocore shares
/// VREA's own <c>AndroidRAM</c> tag, so its group can contain RAM genes; if both
/// VREA_FastRAM and VREA_SlowRAM were ever present they'd still conflict and the gate
/// would correctly refuse.
///
/// No-op (zero overhead) for any pawn without an AAH companion gene, and for the
/// creation dialog (which never seeds our genes). Reflection-only: silently skipped
/// when VREA is absent.
/// </summary>
[HarmonyPatch]
public static class AndroidDialog_AllowArchotechOverrides_Patch
{
    private static FieldInfo _leftChosenGroupsField;

    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.Window_CreateAndroidBase");
        return type != null ? AccessTools.Method(type, "DoBottomButtons", new[] { typeof(Rect) }) : null;
    }

    [HarmonyPrefix]
    public static void Prefix(object __instance, out List<GeneLeftChosenGroup> __state)
    {
        __state = null;

        var groups = LeftChosenGroups(__instance);
        if (groups == null || groups.Count == 0) return;

        var hardware = AAH_DefOf.AAH_Hardware;
        if (hardware == null) return;

        List<GeneLeftChosenGroup> removed = null;
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            var group = groups[i];
            if (group?.leftChosen?.displayCategory != hardware) continue;
            if (HasGenuineInternalConflict(group)) continue; // a real VREA conflict rode along — keep it blocking

            (removed ??= new List<GeneLeftChosenGroup>()).Add(group);
            groups.RemoveAt(i);
        }
        __state = removed;
    }

    [HarmonyPostfix]
    public static void Postfix(object __instance, List<GeneLeftChosenGroup> __state)
    {
        if (__state == null || __state.Count == 0) return;
        LeftChosenGroups(__instance)?.AddRange(__state);
    }

    /// <summary>
    /// True if any two of the group's overridden (VREA) genes conflict with <i>each
    /// other</i> — i.e. a genuine conflict that exists independently of our override.
    /// The leftChosen member is our gene, so checking the overridden members alone
    /// excludes the override edge we mean to neutralise.
    /// </summary>
    private static bool HasGenuineInternalConflict(GeneLeftChosenGroup group)
    {
        var overridden = group.overriddenGenes;
        for (int i = 0; i < overridden.Count; i++)
            for (int j = i + 1; j < overridden.Count; j++)
                if (overridden[i].ConflictsWith(overridden[j]))
                    return true;
        return false;
    }

    private static List<GeneLeftChosenGroup> LeftChosenGroups(object window)
    {
        if (window == null) return null;
        _leftChosenGroupsField ??= AccessTools.Field(typeof(GeneCreationDialogBase), "leftChosenGroups");
        return _leftChosenGroupsField?.GetValue(window) as List<GeneLeftChosenGroup>;
    }
}
