using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

// Shared helpers for the Self-Determination inspiration, used by the worker
// (InspirationWorker_SelfDetermination) and the two behavior-station
// patches. Centralises the inspiration def lookup and the reflection-only VREA
// calls so there is a single resolution point.
//
// VREA is referenced by reflection only (no compile-time dependency). If VREA is
// absent, IsAndroid/IsAwakened resolve to false — there
// are no androids without VREA anyway, so the inspiration is simply never relevant.
public static class SelfDeterminationUtility
{
    // The AAH_SelfDetermination inspiration def, or null if missing.
    public static InspirationDef Def => AAH_DefOf.AAH_SelfDetermination;

    // True if pawn currently has the Self-Determination inspiration active.
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

    // Reflective VREAndroids.Utils.IsAndroid(Pawn); false if VREA/type missing.
    public static bool IsAndroid(Pawn pawn)
    {
        ResolveVreaMethods();
        return _isAndroidMethod != null && pawn != null && (bool)_isAndroidMethod.Invoke(null, new object[] { pawn });
    }

    // Reflective VREAndroids.Utils.IsAwakened(Pawn); false if VREA/type missing.
    // Caution: VREA's implementation returns true for non-androids too (a pawn with no
    // removeWhenAwakened android gene), so callers that care must pair this with
    // IsAndroid.
    public static bool IsAwakened(Pawn pawn)
    {
        ResolveVreaMethods();
        return _isAwakenedMethod != null && pawn != null && (bool)_isAwakenedMethod.Invoke(null, new object[] { pawn });
    }

    // True when the permanent psychic-transceiver reprogramming unlock applies to
    // pawn: the enableTransceiverReprogramming setting is on and
    // the pawn carries the AAH_PsychicTransceiver implant.
    //
    // Thematically the inverse of the inspiration: where the inspiration is the android's
    // own unaided will, the transceiver opens an awakened android to outside (archotech)
    // influence, so it accepts reprogramming it would otherwise refuse. No reflection —
    // the transceiver hediff is our own def.
    public static bool HasReprogrammingImplant(Pawn pawn)
    {
        var settings = ArchotechAndroidHardwareMod.Settings;
        if (settings?.enableTransceiverReprogramming != true) return false;
        if (pawn?.health?.hediffSet == null) return false;

        var transceiver = AAH_HediffDefOf.AAH_PsychicTransceiver;
        return transceiver != null && pawn.health.hediffSet.HasHediff(transceiver);
    }
}
