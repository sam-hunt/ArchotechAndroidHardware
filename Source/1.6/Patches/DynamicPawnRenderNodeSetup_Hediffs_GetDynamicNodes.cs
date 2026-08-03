using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// Vanilla workaround: re-emits render nodes for VREA's stock reactor
// (VREA_Reactor HediffDef) despite its parent class overriding
// Visible to false.
//
// Problem: DynamicPawnRenderNodeSetup_Hediffs.GetDynamicNodes
// filters hediffs by h.Visible before reading
// def.RenderNodeProperties. VREA's Hediff_AndroidPart (the
// reactor's base class) returns false from Visible to keep android
// internals out of the standard health card. So our XPath-added
// renderNodeProperties on VREA_Reactor never reach the pawn render tree,
// even though the def has them.
//
// Why not patch Hediff_AndroidPart.Visible directly: that would also
// expose the reactor in the standard health tab listing, breaking VREA's
// "internals hidden from generic UI" convention. The reactor is already
// shown in VREA's own hardware panel; double-listing would be noise.
//
// Fix: Postfix wraps the iterator. After yielding the original results, it
// re-checks pawn hediffs for VREA_Reactor specifically — if found and
// invisible (i.e., excluded by the original), instantiate its render nodes
// using the same construction logic as the original method.
//
// Scope is intentionally narrow to a single def (rather than "any AAH-styled
// hediff with graphic props"): VREA may add render-node-bearing parts in
// future versions and we don't want to silently unhide them. If we add a
// chest visual to another VREA part later, extend the def allowlist.
//
// Why not target our own AAH reactors here: they extend
// Hediff_AddedPart directly, so Visible defaults to true and their
// render nodes already reach the tree through the original method.
//
// Performance: this looks like a hot path but isn't. GetDynamicNodes
// only runs when PawnRenderTree rebuilds — gated by the tree's
// Resolved flag in TrySetupGraphIfNeeded. Rebuilds fire on
// invalidation events (apparel equip/unequip, hediff add/remove, animation
// changes, gene tracker dirty, body-type swap, portrait hair-colour
// override), not per frame. Between invalidations the cached tree is reused
// and our Postfix never executes. The wrapping cost per invocation is also
// negligible — one state-machine allocation plus a linear pass over the
// pawn's hediff list that the original already iterates, dwarfed by the
// Activator.CreateInstance calls and apparel-layer composition that
// follow on every rebuild.
//
// Removable if: VREA changes its visibility model, or vanilla decouples
// render-node emission from the UI Visible check.
[HarmonyPatch(typeof(DynamicPawnRenderNodeSetup_Hediffs), nameof(DynamicPawnRenderNodeSetup_Hediffs.GetDynamicNodes))]
public static class DynamicPawnRenderNodeSetup_Hediffs_GetDynamicNodes_Patch
{
    private static HediffDef VreaReactorDef => AAH_HediffDefOf.VREA_Reactor;

    [HarmonyPostfix]
    public static void Postfix(
        ref IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> __result,
        Pawn pawn,
        PawnRenderTree tree)
    {
        __result = Wrap(__result, pawn, tree);
    }

    // Wrapping is a separate iterator method (rather than `yield`ing directly
    // from Postfix) so the Postfix above stays a plain void method — keeps
    // Harmony's signature matching unambiguous regardless of how it handles
    // iterator-returning Postfixes.
    private static IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> Wrap(
        IEnumerable<(PawnRenderNode node, PawnRenderNode parent)> original,
        Pawn pawn,
        PawnRenderTree tree)
    {
        foreach (var v in original)
            yield return v;

        var hediffs = pawn?.health?.hediffSet?.hediffs;
        if (hediffs == null) yield break;

        var targetDef = VreaReactorDef;
        if (targetDef == null) yield break;

        for (int i = 0; i < hediffs.Count; i++)
        {
            var h = hediffs[i];
            // Original method already yielded visible hediffs' nodes — skip to avoid duplicates.
            if (h.def != targetDef || h.Visible || !h.def.HasDefinedGraphicProperties) continue;

            foreach (var props in h.def.RenderNodeProperties)
            {
                if (!tree.ShouldAddNodeToTree(props)) continue;
                var node = (PawnRenderNode)Activator.CreateInstance(props.nodeClass, pawn, props, tree);
                node.hediff = h;
                node.bodyPart = h.Part;
                yield return (node, (PawnRenderNode)null);
            }
        }
    }
}
