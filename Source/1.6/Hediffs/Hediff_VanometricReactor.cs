using System.Linq;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Core hediff for the vanometric reactor. Manages a companion gene lifecycle:
///
/// Gene mechanism: A companion gene (AAH_VanometricReactor) shares an exclusion
/// tag with VREA_Power. Biotech's gene override system suppresses VREA_Power
/// while our gene is active, which prevents VREA_Power's enablesNeeds from
/// creating the ReactorPower need. The hediff's disablesNeeds then removes
/// any residual need instance.
///
/// Lifecycle:
///   Install  (PostAdd)         -> adds companion gene as xenogene
///   Runtime                    -> hediff's disablesNeeds suppresses VREA_ReactorPower
///   Removal  (PostRemoved)     -> removes companion gene, destroys reactor body part
///   Load     (PostLoadInit)    -> self-heals gene for saves predating the gene system
///
/// Note: extends Hediff_AddedPart (not VREA's Hediff_AndroidReactor) because we
/// don't need VREA's reactor drain logic. This type mismatch is what triggers
/// the PawnHealthTracker_ShouldBeDowned workaround -- see that patch for details.
/// </summary>
public class Hediff_VanometricReactor : Hediff_AddedPart
{
    private static GeneDef _vanometricPowerGene;
    private static GeneDef VanometricPowerGene =>
        _vanometricPowerGene ??= DefDatabase<GeneDef>.GetNamed("AAH_VanometricReactor", errorOnFail: false);

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

        // Destroy the reactor body part so it shows as missing, like amputating a
        // bionic limb. Without this, the reactor slot reverts to a non-functional
        // natural body part — an inconsistent state where VREA sees a reactor slot
        // but no Hediff_AndroidReactor, causing broken power need behavior.
        // Skipped during replacement surgery where the body part must stay intact.
        if (!SurgeryState.SuppressBodyPartDestruction && pawn?.health?.hediffSet != null && bodyPart != null)
        {
            pawn.health.AddHediff(HediffDefOf.MissingBodyPart, bodyPart);
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        // Save migration: ensures gene exists for saves created before the gene system.
        // Without this, loading an old save would leave the hediff without its companion
        // gene, so VREA_Power would remain active alongside the vanometric reactor.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            AddGeneIfMissing();
    }

    private void AddGeneIfMissing()
    {
        if (pawn?.genes == null || VanometricPowerGene == null) return;
        if (pawn.genes.GenesListForReading.Any(g => g.def == VanometricPowerGene)) return;
        pawn.genes.AddGene(VanometricPowerGene, xenogene: true);
    }

    private void RemoveGeneIfPresent()
    {
        if (pawn?.genes == null || VanometricPowerGene == null) return;
        var gene = pawn.genes.GenesListForReading.FirstOrDefault(g => g.def == VanometricPowerGene);
        if (gene != null)
            pawn.genes.RemoveGene(gene);
    }
}
