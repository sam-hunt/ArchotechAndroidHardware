using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Scenario part that puts a piece of apparel on each starting colonist
// (worn, not dropped into the stockpile — which is all vanilla's
// ScenPart_StartingThing_Defined can do). Used by "Magus of the Abyss" to
// dress its starter in dark-grey marine (power) armor.
//
// Generic over the apparel def, optional stuff, and an optional override colour
// (overrideColor + color, applied via the apparel's
// CompColorable; left at the generated colour otherwise).
//
// Runs from ModifyPawnPostGenerate so it lands after vanilla
// gear generation (we wear on top, dropping any layer-conflicting piece) and is
// visible on the starting-pawn config screen. Idempotent across redress / re-roll.
// Scope it to PlayerStarter in XML.
public class ScenPart_StartingApparelWorn : ScenPart_PawnModifier
{
    private ThingDef apparelDef;
    private ThingDef stuffDef;
    private bool overrideColor;
    private Color color = Color.white;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref apparelDef, "apparelDef");
        Scribe_Defs.Look(ref stuffDef, "stuffDef");
        Scribe_Values.Look(ref overrideColor, "overrideColor", defaultValue: false);
        Scribe_Values.Look(ref color, "color", Color.white);
    }

    public override string Summary(Scenario scen)
    {
        if (apparelDef == null) return null;
        return "Each starting android begins wearing " + apparelDef.label + ".";
    }

    public override bool HasNullDefs() => base.HasNullDefs() || apparelDef == null;

    public override void DoEditInterface(Listing_ScenEdit listing)
    {
        Rect rect = listing.GetScenPartRect(this, RowHeight * 3f);
        if (Widgets.ButtonText(rect.TopPartPixels(RowHeight), apparelDef?.LabelCap ?? "(select apparel)"))
        {
            FloatMenuUtility.MakeMenu(PossibleApparel(),
                d => d.LabelCap.ToString(),
                d => delegate { apparelDef = d; });
        }
        DoPawnModifierEditInterface(rect.BottomPartPixels(RowHeight * 2f));
    }

    private static IEnumerable<ThingDef> PossibleApparel() =>
        DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsApparel);

    public override int GetHashCode() => base.GetHashCode() ^ (apparelDef?.GetHashCode() ?? 0);

    protected override void ModifyPawnPostGenerate(Pawn p, bool redressed)
    {
        if (apparelDef == null || p?.apparel == null) return;

        // Idempotent: a redressed pawn / re-roll may already wear it.
        if (p.apparel.WornApparel.Any(a => a.def == apparelDef)) return;

        var stuff = stuffDef;
        if (stuff == null && apparelDef.MadeFromStuff)
            stuff = GenStuff.DefaultStuffFor(apparelDef);

        if (ThingMaker.MakeThing(apparelDef, stuff) is not Apparel apparel) return;

        apparel.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
        if (overrideColor)
            apparel.TryGetComp<CompColorable>()?.SetColor(color);

        // No map yet during generation, so replaced layers are destroyed, not dropped.
        p.apparel.Wear(apparel, dropReplacedApparel: false);
    }
}
