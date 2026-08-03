using System.Linq;
using Verse;

namespace ArchotechAndroidHardware;

// Core hediff for the psychic transceiver. Manages a companion gene lifecycle:
//
// Gene mechanism: A companion gene (AAH_PsychicTransceiver) shares an exclusion
// tag with VREA_PsychicallyDeaf. Androids are naturally psychically inert; VREA
// encodes this as a gene with PsychicSensitivity factor of zero. Biotech's gene
// override system suppresses that gene while ours is active, removing the zero
// factor. Because that was a x0 *factor* (not a base change), PsychicSensitivity
// then falls back to its 100% StatDef base, on which the hediff's statOffset stacks
// (net = 100% + the offset, not the offset alone). The offset value is the
// transceiverSensitivityOffset setting (see ApplyTransceiverSensitivityOffset).
//
// Lifecycle:
//   Install  (PostAdd)         -> adds companion gene as xenogene
//   Removal  (PostRemoved)     -> removes companion gene
//   Load     (PostLoadInit)    -> re-asserts gene presence if missing (invariant defense)
//
// Note: extends HediffWithComps (not Hediff_AddedPart) because this is a brain
// implant, not a body part replacement. The brain stays intact when this is removed.
public class Hediff_PsychicTransceiver : HediffWithComps
{
    private static GeneDef PsychicTransceiverGene => AAH_GeneDefOf.AAH_PsychicTransceiver;

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
        if (pawn?.genes == null || PsychicTransceiverGene == null) return;
        if (pawn.genes.GenesListForReading.Any(g => g.def == PsychicTransceiverGene)) return;
        pawn.genes.AddGene(PsychicTransceiverGene, xenogene: true);
    }

    private void RemoveGeneIfPresent()
    {
        if (pawn?.genes == null || PsychicTransceiverGene == null) return;
        var gene = pawn.genes.GenesListForReading.FirstOrDefault(g => g.def == PsychicTransceiverGene);
        if (gene != null)
            pawn.genes.RemoveGene(gene);
    }
}
