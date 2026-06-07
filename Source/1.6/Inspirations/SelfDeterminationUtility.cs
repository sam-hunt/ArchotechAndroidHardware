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
    /// <summary>The <c>AAH_SelfDetermination</c> inspiration def, or null if missing.</summary>
    public static InspirationDef Def => AAH_DefOf.AAH_SelfDetermination;

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

    /// <summary>
    /// True when the permanent psychic-transceiver reprogramming unlock applies to
    /// <paramref name="pawn"/>: the <c>enableTransceiverReprogramming</c> setting is on and
    /// the pawn carries the <c>AAH_PsychicTransceiver</c> implant.
    ///
    /// Thematically the inverse of the inspiration: where the inspiration is the android's
    /// own unaided will, the transceiver opens an awakened android to outside (archotech)
    /// influence, so it accepts reprogramming it would otherwise refuse. No reflection —
    /// the transceiver hediff is our own def.
    /// </summary>
    public static bool HasReprogrammingImplant(Pawn pawn)
    {
        var settings = ArchotechAndroidHardwareMod.Settings;
        if (settings == null || !settings.enableTransceiverReprogramming) return false;
        if (pawn?.health?.hediffSet == null) return false;

        var transceiver = AAH_HediffDefOf.AAH_PsychicTransceiver;
        return transceiver != null && pawn.health.hediffSet.HasHediff(transceiver);
    }
}
