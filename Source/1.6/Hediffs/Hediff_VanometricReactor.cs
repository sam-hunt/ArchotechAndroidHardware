using System.Linq;
using RimWorld;
using UnityEngine;
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
///   Load     (PostLoadInit)    -> re-asserts gene presence if missing (invariant defense)
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

    // Lime-green glow sampled directly from the core disk of
    // AAH_VanometricReactor_Chest.png (RGB 222,223,66 — every pixel of the
    // 40px core is identical). The mote texture is white, so this tint
    // becomes the perceived glow colour via MoteGlow's SrcAlpha × One additive
    // blend. The Mote reference is not serialised — it's transient and gets
    // recreated on the first post-load tick.
    private static readonly Color GlowTint = new(0.871f, 0.875f, 0.259f);
    private Mote glowMote;

    public override void PostAdd(DamageInfo? dinfo)
    {
        base.PostAdd(dinfo);
        AddGeneIfMissing();
    }

    // Tick (not TickInterval): in 1.6 Thing.DoTick gates TickInterval behind
    // UpdateRateTicks, which scales up when the pawn is offscreen. With the
    // mote's solidTime=600, an offscreen pawn's TickInterval can fire less
    // often than the maintenance deadline and the mote despawns. Hediff.Tick
    // runs every game tick regardless of update rate — same cadence noctol's
    // CompTick uses for its eye glow.
    public override void Tick()
    {
        base.Tick();
        ReactorGlowMote.Maintain(pawn, ref glowMote, GlowTint);
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
        // Invariant: this hediff requires its companion gene to function correctly.
        // Re-assert presence after load in case another mod has modified gene state —
        // VREA's novel use of genes for hardware state might confuse gene-manipulating mods.
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
