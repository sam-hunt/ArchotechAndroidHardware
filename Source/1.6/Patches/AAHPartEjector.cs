using Verse;

namespace ArchotechAndroidHardware;

// Implemented by AAH_ hediffs that need to preserve state when ejected (e.g., a
// reactor that stores its current energy level on the ejected item). Hediffs
// that don't implement this interface fall through to the default eject path,
// which spawns HediffDef.spawnThingOnRemoved as a fresh item.
public interface ICustomAAHEjection
{
    // Spawn the ejected item at the given position with any state carried over
    // from this hediff. Called instead of the default spawnThingOnRemoved path.
    void EjectCustom(Pawn pawn, IntVec3 position, Map map);
}

// Central dispatch for ejecting items from AAH_ hediffs during part-replacement
// surgery. Keeps VREAPatches.RecipeInstallAndroidPart_ApplyOnPawn_Patch
// agnostic to specific hediff types — each hediff owns its ejection rule.
//
// Default: spawns HediffDef.spawnThingOnRemoved as a fresh item
// (preserves behavior for Vanometric Reactor, Psychic Transceiver, Mnemocore,
// Neutrosynthesizer — all stateless in terms of the ejected item).
//
// Custom: hediffs implementing ICustomAAHEjection handle the spawn
// themselves, typically to transfer runtime state (e.g., Thanatic Reactor's
// current energy level) onto the ejected thing.
public static class AAHPartEjector
{
    public static void Eject(Hediff hediff, Pawn pawn)
    {
        if (hediff == null || pawn == null) return;

        var position = pawn.PositionHeld;
        var map = pawn.MapHeld;
        if (map == null) return;

        if (hediff is ICustomAAHEjection custom)
        {
            custom.EjectCustom(pawn, position, map);
            return;
        }

        if (hediff.def.spawnThingOnRemoved != null)
        {
            var thing = ThingMaker.MakeThing(hediff.def.spawnThingOnRemoved);
            GenPlace.TryPlaceThing(thing, position, map, ThingPlaceMode.Near);
        }
    }
}
