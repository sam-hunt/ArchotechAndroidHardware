using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Scenario part that grants a gene to starting colonists, using the inherited
// chance roll for "some of the time" odds. Used by "Magus of the Abyss" to
// give each starter a 25% chance of red eyes.
//
// The configured gene for that use is VREA_Eyes_Red — not vanilla
// Biotech's Eyes_Red. VREA's GeneDefGenerator.ImpliedGeneDefs postfix
// clones every "convertable" vanilla cosmetic gene (eye colour included) into a
// VREA_-prefixed implied counterpart carrying the hardware-gene background,
// and registers it in allAndroidGenes; that clone is exactly what the
// Android Creation / Behaviorist Station dialogs offer as the "red eyes" hardware
// gene. Adding the same def here makes the in-game gene inspector and those dialogs
// agree. (Implied defs are registered before cross-references resolve, so the
// ScenPartDef XML can reference VREA_Eyes_Red directly.)
//
// Runs from ModifyPawnPostGenerate so the gene is present before the
// config screen renders the pawn (its eye render node then resolves on first draw).
// Added as a xenogene to match how VREA installs hardware genes. Idempotent.
// Scope it to PlayerStarter in XML.
public class ScenPart_StartingGene : ScenPart_PawnModifier
{
    private GeneDef geneDef;
    private bool xenogene = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref geneDef, "geneDef");
        Scribe_Values.Look(ref xenogene, "xenogene", defaultValue: true);
    }

    public override string Summary(Scenario scen)
    {
        if (geneDef == null) return null;
        return "Each starting android has a " + chance.ToStringPercent() + " chance of the "
            + geneDef.label + " gene.";
    }

    public override bool HasNullDefs() => base.HasNullDefs() || geneDef == null;

    public override void DoEditInterface(Listing_ScenEdit listing)
    {
        Rect rect = listing.GetScenPartRect(this, RowHeight * 3f);
        if (Widgets.ButtonText(rect.TopPartPixels(RowHeight), geneDef?.LabelCap ?? "(select gene)"))
        {
            FloatMenuUtility.MakeMenu(DefDatabase<GeneDef>.AllDefsListForReading,
                d => d.LabelCap.ToString(),
                d => delegate { geneDef = d; });
        }
        DoPawnModifierEditInterface(rect.BottomPartPixels(RowHeight * 2f));
    }

    public override int GetHashCode() => base.GetHashCode() ^ (geneDef?.GetHashCode() ?? 0);

    protected override void ModifyPawnPostGenerate(Pawn p, bool redressed)
    {
        if (geneDef == null || p?.genes == null) return;

        // Idempotent across redress / re-roll.
        if (p.genes.GenesListForReading.Any(g => g.def == geneDef)) return;

        p.genes.AddGene(geneDef, xenogene);

        // Cosmetic genes add render nodes; force a rebuild in case the pawn has
        // already been drawn (e.g. re-rolled on the config screen). Harmless and
        // lazy during fresh generation, where the renderer isn't built yet.
        p.Drawer?.renderer?.SetAllGraphicsDirty();
    }
}
