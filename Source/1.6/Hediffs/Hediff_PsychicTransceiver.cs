using System.Linq;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Core hediff for the psychic transceiver. Manages a companion gene lifecycle:
///
/// Gene mechanism: A companion gene (AAH_PsychicTransceiver) shares an exclusion
/// tag with VREA_PsychicallyDeaf. Androids are naturally psychically inert; VREA
/// encodes this as a gene with PsychicSensitivity factor of zero. Biotech's gene
/// override system suppresses that gene while ours is active, removing the zero
/// factor. The hediff's statOffset then provides +20% psychic sensitivity.
///
/// Lifecycle:
///   Install  (PostAdd)         -> adds companion gene as xenogene
///   Removal  (PostRemoved)     -> removes companion gene
///   Load     (PostLoadInit)    -> self-heals gene for saves predating the gene system
///
/// Note: extends HediffWithComps (not Hediff_AddedPart) because this is a brain
/// implant, not a body part replacement. The brain stays intact when this is removed.
/// </summary>
public class Hediff_PsychicTransceiver : HediffWithComps
{
    private static GeneDef _psychicTransceiverGene;
    private static GeneDef PsychicTransceiverGene =>
        _psychicTransceiverGene ??= DefDatabase<GeneDef>.GetNamed("AAH_PsychicTransceiver", errorOnFail: false);

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
        // Save migration: ensures gene exists for saves created before the gene system.
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
