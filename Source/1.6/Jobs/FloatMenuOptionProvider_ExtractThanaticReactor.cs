using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ArchotechAndroidHardware;

// One-click "extract thanatic reactor" right-click option on android corpses
// containing an installed Hediff_ThanaticReactor. Goes beyond vanilla skull
// extraction (which has no float-menu surface — it requires architect-menu
// designation first) so players can discover the feature without opening the
// orders tab.
//
// Adds the AAH_ExtractThanaticReactor designation AND immediately prioritizes
// the extraction job onto the clicking pawn. The designation also makes the
// overlay visible (and lets WorkGiver_ExtractThanaticReactor pick it up if
// the prioritized pawn is interrupted before completion). When the corpse is
// already designated, this provider stays silent — the generic
// FloatMenuOptionProvider_WorkGivers surfaces the same job through our
// WorkGiverDef in that case, so we'd otherwise duplicate the option.
public class FloatMenuOptionProvider_ExtractThanaticReactor : FloatMenuOptionProvider
{
    protected override bool Drafted => false;
    protected override bool Undrafted => true;
    protected override bool Multiselect => false;
    protected override bool RequiresManipulation => true;
    protected override bool MechanoidCanDo => false;

    private static DesignationDef DesignationDef => AAH_DesignationDefOf.AAH_ExtractThanaticReactor;
    private static JobDef JobDef => AAH_JobDefOf.AAH_ExtractThanaticReactor;
    private static HediffDef ReactorHediffDef => AAH_HediffDefOf.AAH_ThanaticReactor;

    public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
    {
        if (clickedThing is not Corpse corpse || corpse.Destroyed) yield break;
        var pawn = context.FirstSelectedPawn;
        if (pawn == null) yield break;

        var designationDef = DesignationDef;
        var jobDef = JobDef;
        var hediffDef = ReactorHediffDef;
        if (designationDef == null || jobDef == null || hediffDef == null) yield break;

        // Already designated → defer to FloatMenuOptionProvider_WorkGivers, which
        // walks all WorkGiverDefs and will surface ours via the existing
        // designation. Adding our own option here would double up.
        if (corpse.Map?.designationManager.DesignationOn(corpse, designationDef) != null)
            yield break;

        var hediff = corpse.InnerPawn?.health?.hediffSet?.GetFirstHediffOfDef(hediffDef);
        if (hediff is not Hediff_ThanaticReactor) yield break;

        var option = new FloatMenuOption(
            "AAH_ExtractThanaticReactorOption".Translate(corpse.LabelShortCap),
            delegate
            {
                if (corpse.Destroyed) return;
                var map = corpse.Map;
                if (map == null) return;
                if (map.designationManager.DesignationOn(corpse, designationDef) == null)
                    map.designationManager.AddDesignation(new Designation(corpse, designationDef));
                var job = JobMaker.MakeJob(jobDef, corpse);
                job.count = 1;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
        yield return FloatMenuUtility.DecoratePrioritizedTask(option, pawn, new LocalTargetInfo(corpse));
    }
}
