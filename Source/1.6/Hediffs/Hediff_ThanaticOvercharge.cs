using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Whole-body combat buff applied when a Thanatic Reactor kill overflows the
/// reactor's energy meter. Mirrors vanilla <c>GoJuiceHigh</c>'s stat envelope
/// (capMods / statOffsets live in XML).
///
/// Duration is managed via <see cref="HediffComp_Disappears"/>. Additional
/// overflow while the buff is active *extends* <c>ticksToDisappear</c> (capped
/// by <see cref="ArchotechAndroidHardwareSettings.thanaticOverchargeCapHours"/>)
/// rather than re-applying the hediff fresh.
///
/// Psyfocus bump (Royalty-gated): +0.15 applies only on the INITIAL application,
/// not on subsequent duration extensions. The magnitude matches go-juice's
/// <c>IngestionOutcomeDoer_OffsetPsyfocus</c> offset; applying it once per buff
/// window also prevents psyfocus farming by chaining small kills — the player
/// has to let the buff expire before they can bank another psyfocus bump.
/// </summary>
public class Hediff_ThanaticOvercharge : HediffWithComps
{
    // Matches go-juice's instant psyfocus offset (Core Drugs/GoJuice.xml).
    private const float PsyfocusBumpOnInitialApply = 0.15f;

    /// <summary>
    /// Apply or extend Thanatic Overcharge on the given pawn.
    /// </summary>
    /// <param name="overflow">Fraction [0,1] of reactor energy that overflowed past max.</param>
    public static void ApplyOrExtend(Pawn pawn, float overflow, ArchotechAndroidHardwareSettings settings)
    {
        if (pawn == null || overflow <= 0f) return;
        var def = AAH_HediffDefOf.AAH_ThanaticOvercharge;
        if (def == null) return;

        // Convert overflow fraction to tick budget: overflow × hoursPerUnit × 2500 ticks/hour
        int addedTicks = Mathf.RoundToInt(overflow * settings.thanaticOverchargeHoursPerUnit * 2500f);
        if (addedTicks <= 0) return;

        // The slider's top notch removes the cap (overflow stacks without limit);
        // int.MaxValue makes the Mathf.Min clamps below no-ops. Real play never
        // accumulates anywhere near that, so the additions can't overflow.
        bool unlimited = settings.thanaticOverchargeCapHours >= ArchotechAndroidHardwareSettings.ThanaticOverchargeCapUnlimited;
        int capTicks = unlimited ? int.MaxValue : Mathf.RoundToInt(settings.thanaticOverchargeCapHours * 2500f);

        var existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
        if (existing != null)
        {
            // Extend — no psyfocus bump. Enforces the anti-farming rule: psyfocus
            // benefit is paid once per discrete buff window, not per kill.
            var comp = existing.TryGetComp<HediffComp_Disappears>();
            if (comp != null)
                comp.ticksToDisappear = Mathf.Min(capTicks, comp.ticksToDisappear + addedTicks);
            return;
        }

        // Fresh application — add the hediff and bank the psyfocus bump if Royalty is active.
        var hediff = HediffMaker.MakeHediff(def, pawn);
        var freshComp = hediff.TryGetComp<HediffComp_Disappears>();
        if (freshComp != null)
            freshComp.ticksToDisappear = Mathf.Min(capTicks, addedTicks);
        pawn.health.AddHediff(hediff);

        // Royalty guard: pawn.psychicEntropy is null without Royalty DLC.
        if (ModsConfig.RoyaltyActive && pawn.psychicEntropy != null)
            pawn.psychicEntropy.OffsetPsyfocusDirectly(PsyfocusBumpOnInitialApply);
    }
}
