using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Shared helpers for the Self-Determination inspiration, used by the worker
/// (<see cref="InspirationWorker_SelfDetermination"/>) and the two behavior-station
/// patches. Centralises the inspiration def lookup and the reflection-only VREA
/// calls so there is a single resolution point.
///
/// VREA is referenced by reflection only (no compile-time dependency). If VREA is
/// absent, <see cref="IsAndroid"/>/<see cref="IsAwakened"/> resolve to false — there
/// are no androids without VREA anyway, so the inspiration is simply never relevant.
/// </summary>
public static class SelfDeterminationUtility
{
    private static InspirationDef _def;
    private static bool _defResolved;

    /// <summary>The <c>AAH_SelfDetermination</c> inspiration def, or null if missing.</summary>
    public static InspirationDef Def
    {
        get
        {
            if (!_defResolved)
            {
                _def = DefDatabase<InspirationDef>.GetNamed("AAH_SelfDetermination", errorOnFail: false);
                _defResolved = true;
            }
            return _def;
        }
    }

    /// <summary>True if <paramref name="pawn"/> currently has the Self-Determination inspiration active.</summary>
    public static bool IsActiveOn(Pawn pawn)
    {
        return pawn != null && Def != null && pawn.InspirationDef == Def;
    }

    private static MethodInfo _isAndroidMethod;
    private static MethodInfo _isAwakenedMethod;
    private static bool _vreaMethodsResolved;

    private static void ResolveVreaMethods()
    {
        if (_vreaMethodsResolved) return;
        _vreaMethodsResolved = true;

        var utils = AccessTools.TypeByName("VREAndroids.Utils");
        if (utils == null) return;
        _isAndroidMethod = AccessTools.Method(utils, "IsAndroid", new[] { typeof(Pawn) });
        _isAwakenedMethod = AccessTools.Method(utils, "IsAwakened", new[] { typeof(Pawn) });
    }

    /// <summary>Reflective <c>VREAndroids.Utils.IsAndroid(Pawn)</c>; false if VREA/type missing.</summary>
    public static bool IsAndroid(Pawn pawn)
    {
        ResolveVreaMethods();
        return _isAndroidMethod != null && pawn != null && (bool)_isAndroidMethod.Invoke(null, new object[] { pawn });
    }

    /// <summary>
    /// Reflective <c>VREAndroids.Utils.IsAwakened(Pawn)</c>; false if VREA/type missing.
    /// Caution: VREA's implementation returns true for non-androids too (a pawn with no
    /// <c>removeWhenAwakened</c> android gene), so callers that care must pair this with
    /// <see cref="IsAndroid"/>.
    /// </summary>
    public static bool IsAwakened(Pawn pawn)
    {
        ResolveVreaMethods();
        return _isAwakenedMethod != null && pawn != null && (bool)_isAwakenedMethod.Invoke(null, new object[] { pawn });
    }
}
