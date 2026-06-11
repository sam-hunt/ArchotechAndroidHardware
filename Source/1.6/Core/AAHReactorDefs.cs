using System.Linq;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Canonical list of the mod's reactor hediff defs — every part that replaces
/// VREA's reactor in the android reactor slot (Vanometric, Thanatic, Grav).
/// Single source of truth so "add a new reactor" touches one place: consumed by
/// the <c>PawnHealthTracker_ShouldBeDowned</c> restoration patch and the
/// <c>ScenPart_StartingAndroidReactor</c> editor picker.
///
/// AAH_GravReactor is Odyssey-gated (its DefOf field is null without Odyssey) and
/// filtered out here. The energy-bar patch (<c>NeedReactorPowerPatchHelpers</c>)
/// keeps a deliberately *narrower* list — only reactors whose Energy backs the
/// power need (<see cref="IAAHReactorEnergy"/>, which Vanometric does not) — so it
/// stays separate rather than deriving from this.
/// </summary>
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
