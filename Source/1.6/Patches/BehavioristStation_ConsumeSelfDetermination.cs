using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// Grants the right payoff moodlet when an android finishes reprogramming itself at
/// VREA's behavior station, branching on <i>why</i> the awakened android was admitted
/// (see <see cref="BehavioristStation_AllowSelfDetermination_Patch"/>):
/// <list type="bullet">
/// <item>Active <c>AAH_SelfDetermination</c> inspiration → the android chose this of its
///   own will: consume the inspiration and grant the positive
///   <c>AAH_SelfDeterminationFulfilled</c> memory.</item>
/// <item>No inspiration but awakened + psychic transceiver → it was reprogrammed under
///   outside (archotech) influence, not its own will: grant the
///   <c>AAH_SelfDeterminationOverridden</c> memory (a decaying-severity unease).</item>
/// </list>
///
/// Patched as a <b>Prefix</b> on <c>Building_AndroidBehavioristStation.FinishAndroidProject()</c>
/// (no parameters) because the method ejects the occupant before it returns — a
/// Postfix would read a null <c>Occupant</c>. FinishAndroidProject only runs when
/// the reprogramming work actually completes, so this is the right "on completion"
/// hook.
///
/// No-op for a normal non-awakened android using the station (neither branch fires).
///
/// Reflection-only: target + <c>Occupant</c> resolved via Harmony reflection; the
/// patch is silently skipped if VREA is absent.
/// </summary>
[HarmonyPatch]
public static class BehavioristStation_ConsumeSelfDetermination_Patch
{
    private static PropertyInfo _occupantProp;
    private static bool _occupantPropResolved;

    private static ThoughtDef _fulfilledThought;
    private static bool _fulfilledThoughtResolved;

    private static ThoughtDef _overriddenThought;
    private static bool _overriddenThoughtResolved;

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

    private static ThoughtDef FulfilledThought
    {
        get
        {
            if (!_fulfilledThoughtResolved)
            {
                _fulfilledThought = DefDatabase<ThoughtDef>.GetNamed("AAH_SelfDeterminationFulfilled", errorOnFail: false);
                _fulfilledThoughtResolved = true;
            }
            return _fulfilledThought;
        }
    }

    private static ThoughtDef OverriddenThought
    {
        get
        {
            if (!_overriddenThoughtResolved)
            {
                _overriddenThought = DefDatabase<ThoughtDef>.GetNamed("AAH_SelfDeterminationOverridden", errorOnFail: false);
                _overriddenThoughtResolved = true;
            }
            return _overriddenThought;
        }
    }
}
