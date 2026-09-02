using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Scenario part that installs an added-part hediff (e.g. an archotech arm) into a
// named body part of each starting colonist. Used by "Magus of the Abyss" to give
// its starter an archotech arm in place of its right shoulder.
//
// Generic over the hediff (hediffDef), the slot's bodyPart def, and
// an optional bodyPartLabel that disambiguates side when a body has more
// than one part of that def — matched against BodyPartRecord.untranslatedCustomLabel
// (the raw English "right shoulder", language-independent), falling back to the
// first matching part. Side is purely a health-tab detail here — RimWorld doesn't
// render left/right limbs separately — but we honour the request precisely.
//
// Runs from ModifyPawnPostGenerate (the same late hook the reactor
// part uses), so the limb is present during the starting-pawn config screen and
// any of the hediff's own PostAdd wiring fires. Idempotent across redress / re-roll.
// Scope it to PlayerStarter in XML.
public class ScenPart_StartingBodyPart : ScenPart_PawnModifier
{
    private HediffDef hediffDef;
    private BodyPartDef bodyPart;

    // Matching token, not display text: compared against
    // BodyPartRecord.untranslatedCustomLabel (raw English "right shoulder") in
    // FindTargetPart. [NoTranslate] keeps it out of the DefInjected surface and
    // makes the game refuse an injected override even if one shipped.
    [NoTranslate]
    private string bodyPartLabel;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref hediffDef, "hediffDef");
        Scribe_Defs.Look(ref bodyPart, "bodyPart");
        Scribe_Values.Look(ref bodyPartLabel, "bodyPartLabel");
    }

    public override string Summary(Scenario scen)
    {
        if (hediffDef == null || bodyPart == null) return null;
        return "AAH_ScenSummaryStartingBodyPart".Translate(hediffDef.label,
            bodyPartLabel.NullOrEmpty() ? bodyPart.label : bodyPartLabel);
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
    // sensibly "replace" a body part. bodyPart / bodyPartLabel stay XML-authored.
    private static IEnumerable<HediffDef> PossibleHediffs() =>
        DefDatabase<HediffDef>.AllDefsListForReading
            .Where(h => h.hediffClass != null && typeof(Hediff_AddedPart).IsAssignableFrom(h.hediffClass));

    public override int GetHashCode() =>
        base.GetHashCode() ^ (hediffDef?.GetHashCode() ?? 0) ^ (bodyPart?.GetHashCode() ?? 0);

    protected override void ModifyPawnPostGenerate(Pawn p, bool redressed)
    {
        if (hediffDef == null || bodyPart == null || p?.health?.hediffSet == null) return;
        if (p.RaceProps?.body == null) return;

        var part = FindTargetPart(p);
        if (part == null) return;

        // Idempotent: a redressed pawn / re-roll may already carry it on this slot.
        if (p.health.hediffSet.hediffs.Any(h => h.def == hediffDef && h.Part == part)) return;

        // AddHediff -> Hediff_AddedPart.PostAdd restores the slot (evicting whatever
        // natural/added part sat there), then runs the hediff's own PostAdd wiring.
        var hediff = HediffMaker.MakeHediff(hediffDef, p, part);
        p.health.AddHediff(hediff, part);
    }

    private BodyPartRecord FindTargetPart(Pawn p)
    {
        var parts = p.RaceProps.body.GetPartsWithDef(bodyPart);
        if (parts.Count == 0) return null;
        if (!bodyPartLabel.NullOrEmpty())
        {
            var match = parts.FirstOrDefault(pr =>
                string.Equals(pr.untranslatedCustomLabel, bodyPartLabel, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return parts[0];
    }
}
