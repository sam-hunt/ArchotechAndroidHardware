using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Whole-body buff applied when a grav reactor's launch-refill overflows the
// energy meter — the reactor was already partly charged at launch, so the
// "wasted" refill discharges as grav heat. Because the default refill is full
// (1.0), the overflow equals the pre-launch charge: a launch on a near-full
// reactor yields the most overcharge, an empty one almost none.
//
// Mirrors Hediff_ThanaticOvercharge in shape — duration via
// HediffComp_Disappears, with repeat launches *extending*
// ticksToDisappear (capped by
// ArchotechAndroidHardwareSettings.gravOverchargeCapHours) rather
// than re-applying the hediff fresh. It carries no psyfocus bump (that is the
// thanatic/psychic theme); grav's payoff is the thermal-vent + agility stat
// envelope defined in the XML stage.
public class Hediff_GravOvercharge : HediffWithComps
{
    // Apply or extend Grav Overcharge on the given pawn.
    // overflow: Fraction [0,1] of reactor energy that overflowed past max on refill.
    public static void ApplyOrExtend(Pawn pawn, float overflow, ArchotechAndroidHardwareSettings settings)
    {
        if (pawn == null || overflow <= 0f) return;
        var def = AAH_HediffDefOf.AAH_GravOvercharge;
        if (def == null) return;

        // Convert overflow fraction to tick budget: overflow × hoursPerUnit × 2500 ticks/hour.
        int addedTicks = Mathf.RoundToInt(overflow * settings.gravOverchargeHoursPerUnit * 2500f);
        if (addedTicks <= 0) return;

        // The slider's top notch removes the cap (launches stack without limit);
        // int.MaxValue makes the Mathf.Min clamps below no-ops. Real play never
        // accumulates anywhere near that, so the additions can't overflow.
        bool unlimited = settings.gravOverchargeCapHours >= ArchotechAndroidHardwareSettings.GravOverchargeCapUnlimited;
        int capTicks = unlimited ? int.MaxValue : Mathf.RoundToInt(settings.gravOverchargeCapHours * 2500f);

        var existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
        if (existing != null)
        {
            // Extend the existing buff window rather than stacking instances.
            var comp = existing.TryGetComp<HediffComp_Disappears>();
            if (comp != null)
                comp.ticksToDisappear = Mathf.Min(capTicks, comp.ticksToDisappear + addedTicks);
            return;
        }

        var hediff = HediffMaker.MakeHediff(def, pawn);
        var freshComp = hediff.TryGetComp<HediffComp_Disappears>();
        if (freshComp != null)
            freshComp.ticksToDisappear = Mathf.Min(capTicks, addedTicks);
        pawn.health.AddHediff(hediff);
    }
}
