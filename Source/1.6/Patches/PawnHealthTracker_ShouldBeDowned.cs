using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// VREA workaround: restores standard downed logic for androids with our reactor.
///
/// Problem: VREA's prefix on ShouldBeDowned checks for Hediff_AndroidReactor via
/// OfType&lt;Hediff_AndroidReactor&gt;(). Our hediff extends Hediff_AddedPart (not their
/// type), so VREA finds no reactor and forces the android permanently downed. We
/// use Hediff_AddedPart instead of Hediff_AndroidReactor because we don't need
/// VREA's reactor drain mechanics -- our reactor provides unlimited power.
///
/// Fix: This postfix runs after VREA's prefix has set __result = true (downed).
/// It checks if the pawn has our hediff and, if so, re-evaluates using the
/// standard capacity-based check (CanBeAwake + CapableOf Moving), which is the
/// same logic VREA uses in its own else branch for reactor-equipped androids.
///
/// Lifecycle context: Active at runtime for every ShouldBeDowned evaluation
/// on a pawn with the vanometric reactor.
///
/// Removable if: VREA checks for reactor presence by def, tag, or interface
/// rather than hardcoding the Hediff_AndroidReactor type in OfType&lt;&gt;.
/// </summary>
[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDowned))]
public static class PawnHealthTracker_ShouldBeDowned_Postfix
{
    private static HediffDef _vanometricReactor;
    private static HediffDef VanometricReactor =>
        _vanometricReactor ??= DefDatabase<HediffDef>.GetNamed("AAH_VanometricReactor", errorOnFail: false);

    [HarmonyPostfix]
    public static void Postfix(ref bool __result, Pawn_HealthTracker __instance, Pawn ___pawn)
    {
        if (!__result)
            return;

        if (VanometricReactor == null)
            return;

        if (___pawn?.health?.hediffSet?.HasHediff(VanometricReactor) != true)
            return;

        // Override VREA's "no reactor found -> downed" decision with the standard
        // capacity-based check. This matches VREA's own else branch for androids that
        // do have a recognized reactor type.
        __result = !__instance.capacities.CanBeAwake ||
                   !__instance.capacities.CapableOf(PawnCapacityDefOf.Moving);
    }
}
