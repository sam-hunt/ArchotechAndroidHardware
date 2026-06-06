using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// Consumes the <c>AAH_SelfDetermination</c> inspiration and grants its payoff
/// moodlet when an inspired android finishes reprogramming itself at VREA's
/// behavior station.
///
/// Patched as a <b>Prefix</b> on <c>Building_AndroidBehavioristStation.FinishAndroidProject()</c>
/// (no parameters) because the method ejects the occupant before it returns — a
/// Postfix would read a null <c>Occupant</c>. FinishAndroidProject only runs when
/// the reprogramming work actually completes, so this is the right "on completion"
/// hook.
///
/// No-op unless the occupant currently has the inspiration, so a normal
/// (non-awakened) android using the station is unaffected.
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
        if (!SelfDeterminationUtility.IsActiveOn(occupant)) return;

        occupant.mindState?.inspirationHandler?.EndInspiration(SelfDeterminationUtility.Def);

        var thought = FulfilledThought;
        if (thought != null)
            occupant.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
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
}
