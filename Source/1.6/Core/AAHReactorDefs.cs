using System.Linq;
using Verse;

namespace ArchotechAndroidHardware;

// Canonical list of the mod's reactor hediff defs — every part that replaces
// VREA's reactor in the android reactor slot (Vanometric, Thanatic, Grav).
// Single source of truth so "add a new reactor" touches one place: consumed by
// the PawnHealthTracker_ShouldBeDowned restoration patch and the
// ScenPart_StartingAndroidReactor editor picker.
//
// AAH_GravReactor is Odyssey-gated (its DefOf field is null without Odyssey) and
// filtered out here. The energy-bar patch (NeedReactorPowerPatchHelpers)
// keeps a deliberately *narrower* list — only reactors whose Energy backs the
// power need (IAAHReactorEnergy, which Vanometric does not) — so it
// stays separate rather than deriving from this.
public static class AAHReactorDefs
{
    private static HediffDef[] _all;

    public static HediffDef[] All => _all ??= new[]
    {
        AAH_HediffDefOf.AAH_VanometricReactor,
        AAH_HediffDefOf.AAH_ThanaticReactor,
        AAH_HediffDefOf.AAH_GravReactor,
    }.Where(d => d != null).ToArray();
}
