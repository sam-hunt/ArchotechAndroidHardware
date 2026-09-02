using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Scenario part that installs an added-part hediff (e.g. an archotech arm) into a
// named body part of each starting colonist. Used by "Magus of the Abyss" to give
// its starter an archotech arm in place of a shoulder.
//
// Generic over the hediff (hediffDef) and the slot's bodyPart def. When a body has
// several parts of that def (left/right shoulder) the first in body-def order is
// used; side is purely a health-tab detail, RimWorld doesn't render limbs by side.
//
// Runs from ModifyPawnPostGenerate (the same late hook the reactor
// part uses), so the limb is present during the starting-pawn config screen and
// any of the hediff's own PostAdd wiring fires. Idempotent across redress / re-roll.
// Scope it to PlayerStarter in XML.
public class ScenPart_StartingBodyPart : ScenPart_PawnModifier
{
    private HediffDef hediffDef;
    private BodyPartDef bodyPart;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref hediffDef, "hediffDef");
        Scribe_Defs.Look(ref bodyPart, "bodyPart");
    }

    public override string Summary(Scenario scen)
    {
        if (hediffDef == null || bodyPart == null) return null;
        return "AAH_ScenSummaryStartingBodyPart".Translate(hediffDef.label, bodyPart.label);
    }

    public override bool HasNullDefs() => base.HasNullDefs() || hediffDef == null || bodyPart == null;

    public override void DoEditInterface(Listing_ScenEdit listing)
    {
        Rect rect = listing.GetScenPartRect(this, RowHeight * 3f);
        if (Widgets.ButtonText(rect.TopPartPixels(RowHeight), hediffDef?.LabelCap ?? "AAH_ScenEditSelectPart".Translate()))
        {
            FloatMenuUtility.MakeMenu(PossibleHediffs(),
                d => d.LabelCap.ToString(),
                d => delegate { hediffDef = d; });
        }
        DoPawnModifierEditInterface(rect.BottomPartPixels(RowHeight * 2f));
    }

    // Added-part hediffs (bionics / archotech prosthetics) — the things that
    // sensibly "replace" a body part. bodyPart stays XML-authored.
    private static IEnumerable<HediffDef> PossibleHediffs() =>
        DefDatabase<HediffDef>.AllDefsListForReading
            .Where(h => h.hediffClass != null && typeof(Hediff_AddedPart).IsAssignableFrom(h.hediffClass));

    public override int GetHashCode() =>
        base.GetHashCode() ^ (hediffDef?.GetHashCode() ?? 0) ^ (bodyPart?.GetHashCode() ?? 0);

    protected override void ModifyPawnPostGenerate(Pawn p, bool redressed)
    {
        if (hediffDef == null || bodyPart == null || p?.health?.hediffSet == null) return;
        if (p.RaceProps?.body == null) return;

        var part = p.RaceProps.body.GetPartsWithDef(bodyPart).FirstOrDefault();
        if (part == null) return;

        // Idempotent: a redressed pawn / re-roll may already carry it on this slot.
        if (p.health.hediffSet.hediffs.Any(h => h.def == hediffDef && h.Part == part)) return;

        // AddHediff -> Hediff_AddedPart.PostAdd restores the slot (evicting whatever
        // natural/added part sat there), then runs the hediff's own PostAdd wiring.
        var hediff = HediffMaker.MakeHediff(hediffDef, p, part);
        p.health.AddHediff(hediff, part);
    }
}
