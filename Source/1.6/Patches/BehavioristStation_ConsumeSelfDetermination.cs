using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// Grants the right payoff moodlet when an android finishes reprogramming itself at
// VREA's behavior station, branching on why the awakened android was admitted
// (see BehavioristStation_AllowSelfDetermination_Patch):
// - Active AAH_SelfDetermination inspiration → the android chose this of its
//   own will: consume the inspiration and grant the positive
//   AAH_SelfDeterminationFulfilled memory.
// - No inspiration but awakened + psychic transceiver → it was reprogrammed under
//   outside (archotech) influence, not its own will: grant the
//   AAH_SelfDeterminationOverridden memory (a decaying-severity unease).
//
// Patched as a Prefix on Building_AndroidBehavioristStation.FinishAndroidProject()
// (no parameters) because the method ejects the occupant before it returns — a
// Postfix would read a null Occupant. FinishAndroidProject only runs when
// the reprogramming work actually completes, so this is the right "on completion"
// hook.
//
// No-op for a normal non-awakened android using the station (neither branch fires).
//
// Reflection-only: target + Occupant resolved via Harmony reflection; the
// patch is silently skipped if VREA is absent.
[HarmonyPatch]
public static class BehavioristStation_ConsumeSelfDetermination_Patch
{
    private static PropertyInfo _occupantProp;
    private static bool _occupantPropResolved;

    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.Building_AndroidBehavioristStation");
        return type != null ? AccessTools.Method(type, "FinishAndroidProject") : null;
    }

    [HarmonyPrefix]
    public static void Prefix(object __instance)
    {
        var occupant = GetOccupant(__instance);
        if (occupant == null) return;

        // Genuine self-determination: the android chose this of its own will. Consume the
        // inspiration and grant the positive payoff.
        if (SelfDeterminationUtility.IsActiveOn(occupant))
        {
            occupant.mindState?.inspirationHandler?.EndInspiration(SelfDeterminationUtility.Def);

            var fulfilled = FulfilledThought;
            if (fulfilled != null)
                occupant.needs?.mood?.thoughts?.memories?.TryGainMemory(fulfilled);
            return;
        }

        // Transceiver-driven reprogramming with no active inspiration: an awakened android
        // was reprogrammed under outside (archotech) influence, not its own will, and dimly
        // senses the override. The IsAwakened guard means a non-awakened android being
        // reprogrammed normally gets no thought.
        if (SelfDeterminationUtility.IsAwakened(occupant)
            && SelfDeterminationUtility.HasReprogrammingImplant(occupant))
        {
            var overridden = OverriddenThought;
            if (overridden != null)
                occupant.needs?.mood?.thoughts?.memories?.TryGainMemory(overridden);
        }
    }

    private static Pawn GetOccupant(object station)
    {
        if (station == null) return null;
        if (!_occupantPropResolved)
        {
            _occupantProp = AccessTools.Property(station.GetType(), "Occupant");
            _occupantPropResolved = true;
        }
        return _occupantProp?.GetValue(station) as Pawn;
    }

    private static ThoughtDef FulfilledThought => AAH_DefOf.AAH_SelfDeterminationFulfilled;
    private static ThoughtDef OverriddenThought => AAH_DefOf.AAH_SelfDeterminationOverridden;
}
