using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

// Scenario part that drives every other faction hostile at game start. Vanilla
// has no equivalent part, so this thin custom one backs the "Magus of the Abyss"
// framing: an android bent on exterminating organic life starts friends with no
// one.
//
// Goodwill factions are pushed to the -100 floor — TryAffectGoodwillWith
// clamps the oversized delta and CheckKindThresholds flips the relation to
// Hostile. Permanent-enemy / goodwill-less factions (mechanoids, insects,
// entities) are already fixed-hostile and left alone. This only sets the
// *starting* relation; the natural-goodwill system may drift some factions back
// over time, which suits the playstyle (the host keeps re-angering everyone by
// killing to feed its reactor anyway).
public class ScenPart_AllFactionsHostile : ScenPart
{
    public override string Summary(Scenario scen)
    {
        return "AAH_ScenSummaryAllFactionsHostile".Translate();
    }

    public override void PostGameStart()
    {
        var player = Faction.OfPlayerSilentFail;
        if (player == null) return;

        foreach (var faction in Find.FactionManager.AllFactionsListForReading)
        {
            if (faction == player || faction.IsPlayer) continue;
            // Permanent enemies and other goodwill-less factions can't be moved by
            // goodwill and are already hostile by definition.
            if (!faction.HasGoodwill) continue;
            if (faction.RelationKindWith(player) == FactionRelationKind.Hostile) continue;

            faction.TryAffectGoodwillWith(player, -200, canSendMessage: false, canSendHostilityLetter: false);
        }
    }
}
