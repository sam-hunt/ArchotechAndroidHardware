using System.Linq;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Core hediff for the archotech mnemocore. Manages a companion gene lifecycle:
///
/// Gene mechanism: A companion gene (AAH_ArchotechMnemocore) shares exclusion tags
/// with VREA_MemoryProcessing (AAH_AndroidMemory), VREA_FastRAM/VREA_SlowRAM
/// (AndroidRAM), and VREA_MemoryDecay (AAH_AndroidMemoryDecay). Biotech's gene
/// override system suppresses those genes while ours is active. With
/// VREA_MemoryProcessing suppressed, its enablesNeeds for VREA_MemorySpace doesn't
/// fire. The hediff's disablesNeeds provides a belt-and-suspenders safety net.
///
/// Lifecycle:
///   Install  (PostAdd)         -> adds companion gene as xenogene
///   Removal  (PostRemoved)     -> removes companion gene
///   Load     (PostLoadInit)    -> re-asserts gene presence if missing (invariant defense)
///
/// Note: extends HediffWithComps (not Hediff_AddedPart) because this is a brain
/// implant, not a body part replacement. The brain stays intact when this is removed.
/// </summary>
public class Hediff_ArchotechMnemocore : HediffWithComps
{
    private static GeneDef MnemocoreGene => AAH_GeneDefOf.AAH_ArchotechMnemocore;

    public override void PostAdd(DamageInfo? dinfo)
    {
        base.PostAdd(dinfo);
        AddGeneIfMissing();
    }

    public override void PostRemoved()
    {
        base.PostRemoved();
        RemoveGeneIfPresent();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        // Invariant: this hediff requires its companion gene to function correctly.
        // Re-assert presence after load in case another mod has modified gene state —
        // VREA's novel use of genes for hardware state might confuse gene-manipulating mods.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            AddGeneIfMissing();
    }

    private void AddGeneIfMissing()
    {
        if (pawn?.genes == null || MnemocoreGene == null) return;
        if (pawn.genes.GenesListForReading.Any(g => g.def == MnemocoreGene)) return;
        pawn.genes.AddGene(MnemocoreGene, xenogene: true);
    }

    private void RemoveGeneIfPresent()
    {
        if (pawn?.genes == null || MnemocoreGene == null) return;
        var gene = pawn.genes.GenesListForReading.FirstOrDefault(g => g.def == MnemocoreGene);
        if (gene != null)
            pawn.genes.RemoveGene(gene);
    }
}
