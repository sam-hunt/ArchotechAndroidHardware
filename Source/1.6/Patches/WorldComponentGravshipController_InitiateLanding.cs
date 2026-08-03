using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// Refills every grav reactor that travelled on a gravship, at the moment the
// ship lands at its destination.
//
// Hook: postfix on Odyssey's
// Verse.WorldComponent_GravshipController.InitiateLanding — called
// once per landing, after travel completes, regardless of positive/negative
// outcome. Negative outcomes (crash / rough landings) affect arrival fidelity
// (damage, scatter, etc.) but the ship still arrives, so InitiateLanding
// still fires. This timing intentionally trails the destination picker and
// the travel animation, so:
//
//   • Ritual interrupted before completion → Apply early-returned, no
//     destination picker opens, no launch, no landing → no refill. ✓
//   • Ritual completes but player cancels at the destination picker → no
//     takeoff → no landing → no refill. ✓
//   • Ritual completes, ship launches, ship arrives (clean landing) → refill. ✓
//   • Ritual completes, ship launches, ship arrives (crash landing) → refill. ✓
//
// Manifest of who/what was on the ship: Gravship.Things and
// Gravship.Pawns. The Gravship object captures the contents at
// takeoff and carries them through travel, so reading these collections at
// landing gives the authoritative "actually went on the trip" set — no
// substructure intersection required.
//
// Container traversal: a reactor inside a pawn's inventory, a colonist's
// carry tracker, or a storage building (crate / shelf with inner container)
// won't appear directly in Gravship.Things — only the
// outermost holder does. ThingOwnerUtility.GetAllThingsRecursively
// walks every nested IThingHolder for us, collapsing all those
// scopes into a single recursive scan per holder.
//
// Odyssey types live in the main Assembly-CSharp.dll (the DLC ships no
// separate assembly), so direct C# references to Gravship etc.
// are safe — the types are present even on installs without Odyssey
// ownership (only the Defs are gated). TargetMethod still uses TypeByName
// to match the rest of the patch set's posture: a future rename silently
// no-ops the patch rather than crashing at load.
//
// Removable if: Odyssey exposes a public "gravship landed" event with the
// Gravship attached, or the grav reactor's refill semantics change.
[HarmonyPatch]
public static class WorldComponentGravshipController_InitiateLanding_Patch
{
    private static HediffDef GravReactorHediffDef => AAH_HediffDefOf.AAH_GravReactor;

    static MethodBase TargetMethod()
    {
        // The controller lives in the Verse namespace despite being a RimWorld
        // gameplay component — see WorldComponent_GravshipController in the
        // type listing. TypeByName tolerates either path; we use the actual
        // declared one.
        var type = AccessTools.TypeByName("Verse.WorldComponent_GravshipController");
        return type != null ? AccessTools.Method(type, "InitiateLanding") : null;
    }

    [HarmonyPostfix]
    public static void Postfix(Gravship gravship)
    {
        if (gravship == null) return;

        var hediffDef = GravReactorHediffDef;
        if (hediffDef == null) return;

        var buffer = new List<Thing>();

        // Pawns aboard: refill the installed reactor hediff if present, then
        // walk the pawn's holdings (inventory + carry tracker) for item
        // reactors the colonist is hauling.
        foreach (var pawn in gravship.Pawns)
        {
            if (pawn == null) continue;

            if (pawn.health?.hediffSet?.GetFirstHediffOfDef(hediffDef) is Hediff_GravReactor installed)
                installed.Notify_RechargedByLaunch();

            buffer.Clear();
            ThingOwnerUtility.GetAllThingsRecursively(pawn, buffer, allowUnreal: true);
            for (int i = 0; i < buffer.Count; i++)
                if (buffer[i] is GravReactorThing carried)
                    carried.storedEnergy = 1f;
        }

        // Things aboard: direct hit if it's a reactor item (e.g., dropped on
        // the gravship floor before launch), plus a recursive walk for
        // anything stored inside a container building.
        foreach (var thing in gravship.Things)
        {
            if (thing == null) continue;

            if (thing is GravReactorThing direct)
                direct.storedEnergy = 1f;

            if (thing is IThingHolder holder)
            {
                buffer.Clear();
                ThingOwnerUtility.GetAllThingsRecursively(holder, buffer, allowUnreal: true);
                for (int i = 0; i < buffer.Count; i++)
                    if (buffer[i] is GravReactorThing nested)
                        nested.storedEnergy = 1f;
            }
        }
    }
}
