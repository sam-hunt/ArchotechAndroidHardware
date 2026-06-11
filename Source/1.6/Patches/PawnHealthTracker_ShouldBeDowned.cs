using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// VREA workaround: restores standard downed logic for androids with any AAH_
/// reactor replacement (Vanometric, Thanatic, …).
///
/// Problem: VREA's prefix on ShouldBeDowned checks for Hediff_AndroidReactor via
/// OfType&lt;Hediff_AndroidReactor&gt;(). Our reactor hediffs extend Hediff_AddedPart
/// (not VREA's type), so VREA finds no reactor and forces the android permanently
/// downed. We use Hediff_AddedPart instead of Hediff_AndroidReactor because (a)
/// we keep the mod reflection-only (no VREAndroids.dll compile-time dep) and (b)
/// Vanometric has no drain at all while Thanatic's drain math is reimplemented.
///
/// Fix: After VREA's prefix sets __result = true (downed), check whether the
/// pawn has any known AAH_ reactor hediff. If so, re-evaluate using the standard
/// capacity-based check (CanBeAwake + CapableOf Moving), matching VREA's own
/// else branch for reactor-equipped androids.
///
/// Note on Thanatic specifically: when Thanatic's Energy reaches 0, the hediff's
/// TickInterval calls pawn.Kill() directly — the pawn dies rather than gets
/// downed. This patch ensures the pawn stays upright while Energy > 0; the
/// death transition happens in Hediff_ThanaticReactor, not here.
///
/// Lifecycle context: Active at runtime for every ShouldBeDowned evaluation
/// on a pawn with any AAH_ reactor hediff installed.
///
/// Removable if: VREA checks for reactor presence by def, tag, or interface
/// rather than hardcoding the Hediff_AndroidReactor type in OfType&lt;&gt;.
/// </summary>
[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDowned))]
public static class PawnHealthTracker_ShouldBeDowned_Postfix
{
    [HarmonyPostfix]
    public static void Postfix(ref bool __result, Pawn_HealthTracker __instance, Pawn ___pawn)
    {
        if (!__result) return;
        if (___pawn?.health?.hediffSet == null) return;

        // Canonical reactor set (see AAHReactorDefs) — add a new reactor there.
        var defs = AAHReactorDefs.All;
        if (defs.Length == 0) return;

        bool hasAahReactor = false;
        for (int i = 0; i < defs.Length; i++)
        {
            if (___pawn.health.hediffSet.HasHediff(defs[i]))
            {
                hasAahReactor = true;
                break;
            }
        }
        if (!hasAahReactor) return;

        // Override VREA's "no reactor found -> downed" decision with the standard
        // capacity-based check. This matches VREA's own else branch for androids that
        // do have a recognized reactor type.
        __result = !__instance.capacities.CanBeAwake ||
                   !__instance.capacities.CapableOf(PawnCapacityDefOf.Moving);
    }
}
