using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Scenario part that fits a reactor hediff into the android reactor slot of each
// starting colonist, replacing whatever reactor is already there. Used by the
// "Magus of the Abyss" scenario to guarantee its lone starter begins with a
// thanatic reactor installed.
//
// Runs from ModifyPawnPostGenerate (Scenario.Notify_PawnGenerated),
// the latest pawn-generation hook — it fires after VREA's
// Gene_SyntheticBody.PostAdd has installed the stock VREA_Reactor.
// We take that reactor's body part as the slot and add our hediff there;
// Hediff_AddedPart.PostAdd calls RestorePart on the slot,
// which removes the stock reactor cleanly (its PostRemoved spawns nothing). The
// companion gene and a full starting charge are seeded by the reactor hediff's
// own PostAdd (Hediff_ThanaticReactor.PostAdd).
//
// Scope it to PlayerStarter in XML: the only humanlike pawns with a
// VREA reactor slot that we want to touch are the player's androids. The
// VREA_Reactor guard below means a non-android starter is simply skipped, but
// without the context gate this would also re-fit enemy android raiders.
public class ScenPart_StartingAndroidReactor : ScenPart_PawnModifier
{
    private HediffDef hediffDef;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref hediffDef, "hediffDef");
    }

    public override string Summary(Scenario scen)
    {
        if (hediffDef == null) return null;
        return "Each starting android begins with " + hediffDef.label + " installed in its reactor slot.";
    }

    public override bool HasNullDefs() => base.HasNullDefs() || hediffDef == null;

    // Scenario-editor configurability: pick which reactor the starter is fitted
    // with, plus the inherited chance / pawn-generation context controls. Offered
    // reactors come from the canonical AAHReactorDefs list, so a future reactor
    // shows up here automatically.
    public override void DoEditInterface(Listing_ScenEdit listing)
    {
        Rect rect = listing.GetScenPartRect(this, RowHeight * 3f);
        if (Widgets.ButtonText(rect.TopPartPixels(RowHeight), hediffDef?.LabelCap ?? "(select reactor)"))
        {
            FloatMenuUtility.MakeMenu(AAHReactorDefs.All,
                d => d.LabelCap.ToString(),
                d => delegate { hediffDef = d; });
        }
        DoPawnModifierEditInterface(rect.BottomPartPixels(RowHeight * 2f));
    }

    public override int GetHashCode() => base.GetHashCode() ^ (hediffDef?.GetHashCode() ?? 0);

    protected override void ModifyPawnPostGenerate(Pawn p, bool redressed)
    {
        if (hediffDef == null || p?.health?.hediffSet == null) return;

        // Idempotent: a redressed pawn may already carry the reactor.
        if (p.health.hediffSet.HasHediff(hediffDef)) return;

        // The reactor slot is wherever the stock VREA reactor sits. Requiring one
        // restricts the swap to actual androids — a pawn with no reactor slot
        // shouldn't have one fabricated for it.
        var stockReactor = AAH_HediffDefOf.VREA_Reactor != null
            ? p.health.hediffSet.GetFirstHediffOfDef(AAH_HediffDefOf.VREA_Reactor)
            : null;
        var slot = stockReactor?.Part;
        if (slot == null) return;

        // AddHediff -> Hediff_AddedPart.PostAdd restores the slot (dropping the
        // stock reactor), then our reactor's PostAdd seeds full charge + gene.
        var hediff = HediffMaker.MakeHediff(hediffDef, p, slot);
        p.health.AddHediff(hediff, slot);
    }
}
