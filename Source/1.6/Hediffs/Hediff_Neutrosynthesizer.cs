using System.Linq;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Core hediff for the neutrosynthesizer. Manages a companion gene lifecycle and
/// drives neutroamine recovery.
///
/// Gene mechanism: A companion gene (AAH_Neutrosynthesizer) shares an exclusion
/// tag with VREA_NeutroSynthesis. Biotech's gene override system suppresses
/// VREA_NeutroSynthesis while our gene is active, replacing the subroutine's
/// slow 0.05/day recovery with this hediff's 0.3/day per kidney.
///
/// Neutro recovery: Each tick, reduces VREA_NeutroLoss severity by 0.3/day.
/// With two kidneys replaced, each hediff ticks independently for 0.6/day total,
/// exceeding human blood recovery (0.5/day).
///
/// Lifecycle:
///   Install  (PostAdd)         -> adds companion gene as xenogene
///   Runtime  (Tick)            -> reduces VREA_NeutroLoss severity
///   Removal  (PostRemoved)     -> removes companion gene, destroys kidney body part
///   Load     (PostLoadInit)    -> self-heals gene for saves predating the gene system
///
/// Note: extends Hediff_AddedPart because this is a kidney replacement (like VREA's
/// neutrofilter). On removal, the kidney slot becomes missing.
/// </summary>
public class Hediff_Neutrosynthesizer : Hediff_AddedPart
{
    private const float SeverityPerDay = 0.3f;
    private static readonly float SeverityPerTick = SeverityPerDay / GenDate.TicksPerDay;

    private static GeneDef _neutrosynthesizerGene;
    private static GeneDef NeutrosynthesizerGene =>
        _neutrosynthesizerGene ??= DefDatabase<GeneDef>.GetNamed("AAH_Neutrosynthesizer", errorOnFail: false);

    private static HediffDef _neutroLossDef;
    private static HediffDef NeutroLossDef =>
        _neutroLossDef ??= DefDatabase<HediffDef>.GetNamed("VREA_NeutroLoss", errorOnFail: false);

    public override void Tick()
    {
        base.Tick();

        if (NeutroLossDef == null) return;

        var neutroLoss = pawn.health.hediffSet.GetFirstHediffOfDef(NeutroLossDef);
        if (neutroLoss != null)
            neutroLoss.Severity -= SeverityPerTick;
    }

    public override void PostAdd(DamageInfo? dinfo)
    {
        base.PostAdd(dinfo);
        AddGeneIfMissing();
    }

    public override void PostRemoved()
    {
        var bodyPart = Part;
        base.PostRemoved();
        RemoveGeneIfPresent();

        // Destroy the kidney body part so it shows as missing, like removing a
        // bionic organ. Without this, the kidney slot reverts to a natural body part
        // which is inconsistent for an archotech organ that replaced it entirely.
        // Skipped during replacement surgery where the body part must stay intact.
        if (!SurgeryState.SuppressBodyPartDestruction && pawn?.health?.hediffSet != null && bodyPart != null)
            pawn.health.AddHediff(HediffDefOf.MissingBodyPart, bodyPart);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        // Save migration: ensures gene exists for saves created before the gene system.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            AddGeneIfMissing();
    }

    private void AddGeneIfMissing()
    {
        if (pawn?.genes == null || NeutrosynthesizerGene == null) return;
        if (pawn.genes.GenesListForReading.Any(g => g.def == NeutrosynthesizerGene)) return;
        pawn.genes.AddGene(NeutrosynthesizerGene, xenogene: true);
    }

    private void RemoveGeneIfPresent()
    {
        if (pawn?.genes == null || NeutrosynthesizerGene == null) return;
        // Kidneys are paired — a second Neutrosynthesizer may still be installed on the
        // other slot. `this` has already been removed from hediffSet before PostRemoved
        // runs, so any remaining match indicates a sibling hediff that still needs the gene.
        if (pawn.health?.hediffSet?.hediffs.Any(h => h != this && h.def == def) == true)
            return;
        var gene = pawn.genes.GenesListForReading.FirstOrDefault(g => g.def == NeutrosynthesizerGene);
        if (gene != null)
            pawn.genes.RemoveGene(gene);
    }
}
