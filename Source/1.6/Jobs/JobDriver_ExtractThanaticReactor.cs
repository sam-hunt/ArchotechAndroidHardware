using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ArchotechAndroidHardware;

/// <summary>
/// Walks a colonist to a marked android corpse and extracts the installed
/// thanatic reactor. Mirrors the vanilla skull-extraction flow
/// (JobDriver_ExtractSkull): reserve + goto + wait-with-progress-bar + do.
///
/// The actual reactor recovery (spawning the item with preserved energy,
/// removing the hediff, labeling the missing body part) is delegated to
/// <see cref="Hediff_ThanaticReactor.ExtractFromCorpse"/> so this driver
/// stays purely about job control flow.
/// </summary>
public class JobDriver_ExtractThanaticReactor : JobDriver
{
    private const int ExtractionTimeTicks = 480; // 8 seconds — slower than skull (180) to read as deliberate archotech work

    private static DesignationDef _designationDef;
    private static DesignationDef DesignationDef =>
        _designationDef ??= DefDatabase<DesignationDef>.GetNamed("AAH_ExtractThanaticReactor", errorOnFail: false);

    private static HediffDef _reactorHediffDef;
    private static HediffDef ReactorHediffDef =>
        _reactorHediffDef ??= DefDatabase<HediffDef>.GetNamed("AAH_ThanaticReactor", errorOnFail: false);

    private Corpse Corpse => (Corpse)job.targetA.Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => Corpse == null || Corpse.Destroyed || !Corpse.Spawned);
        this.FailOn(() => DesignationDef == null
            || Map.designationManager.DesignationOn(Corpse, DesignationDef) == null);
        // Bail if the reactor hediff is already gone — could happen if a sibling
        // job extracted in parallel, or another mod stripped the hediff.
        this.FailOn(() => ReactorHediffDef == null
            || Corpse.InnerPawn?.health?.hediffSet?.GetFirstHediffOfDef(ReactorHediffDef) == null);

        yield return Toils_Reserve.Reserve(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);

        var extractToil = Toils_General.Wait(ExtractionTimeTicks);
        extractToil.WithProgressBarToilDelay(TargetIndex.A);
        extractToil.PlaySustainerOrSound(SoundDefOf.Recipe_Surgery);
        yield return extractToil;

        yield return Toils_General.Do(delegate
        {
            var corpse = Corpse;
            if (corpse == null || ReactorHediffDef == null) return;

            var hediff = corpse.InnerPawn?.health?.hediffSet?.GetFirstHediffOfDef(ReactorHediffDef)
                as Hediff_ThanaticReactor;
            if (hediff == null) return;

            hediff.ExtractFromCorpse(corpse);

            var designationDef = DesignationDef;
            if (designationDef != null)
            {
                var designation = corpse.Map?.designationManager.DesignationOn(corpse, designationDef);
                if (designation != null)
                    corpse.Map.designationManager.RemoveDesignation(designation);
            }
        });
    }
}
