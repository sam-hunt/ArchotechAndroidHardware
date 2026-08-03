using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ArchotechAndroidHardware;

// Scans for corpses tagged with the AAH_ExtractThanaticReactor designation and
// dispatches a JobDriver_ExtractThanaticReactor to a willing worker. Mirrors
// vanilla's WorkGiver_ExtractSkull / Designation pattern: the player adds the
// designation via the per-corpse gizmo (see Hediff_ThanaticReactor.GetGizmos),
// and any colonist on BasicWorker work picks the job up.
public class WorkGiver_ExtractThanaticReactor : WorkGiver_Scanner
{
    private static DesignationDef DesignationDef => AAH_DesignationDefOf.AAH_ExtractThanaticReactor;
    private static JobDef JobDef => AAH_JobDefOf.AAH_ExtractThanaticReactor;
    private static HediffDef ReactorHediffDef => AAH_HediffDefOf.AAH_ThanaticReactor;

    public override PathEndMode PathEndMode => PathEndMode.OnCell;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        var designationDef = DesignationDef;
        if (designationDef == null) yield break;
        foreach (var designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(designationDef))
            yield return designation.target.Thing;
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        var designationDef = DesignationDef;
        return designationDef == null || !pawn.Map.designationManager.AnySpawnedDesignationOfDef(designationDef);
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!(t is Corpse { Destroyed: false } corpse)) return false;
        var designationDef = DesignationDef;
        if (designationDef == null) return false;
        if (corpse.Map.designationManager.DesignationOn(t, designationDef) == null) return false;
        if (ReactorHediffDef == null) return false;
        if (corpse.InnerPawn?.health?.hediffSet?.GetFirstHediffOfDef(ReactorHediffDef) is not Hediff_ThanaticReactor)
            return false;
        if (!pawn.CanReserve(t, 1, -1, null, forced)) return false;
        return true;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var jobDef = JobDef;
        if (jobDef == null) return null;
        var job = JobMaker.MakeJob(jobDef, t);
        job.count = 1;
        return job;
    }
}
