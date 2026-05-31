using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Shared opacity logic for the reactor core glow, consumed by both render paths
/// (the <see cref="ReactorGlowMote"/> mote overlay and the
/// <see cref="PawnRenderNodeWorker_ReactorGlow"/> body-attachment node) so they
/// fade identically.
///
/// The glow alpha tracks the android's power-need percentage when the
/// <c>scaleReactorGlowByPower</c> setting is on (default), reading
/// <see cref="Need.CurLevelPercentage"/> on VREA's <c>VREA_ReactorPower</c> need.
/// That single source unifies every reactor type without per-reactor branching,
/// because <see cref="VREAPatches.NeedReactorPowerPatchHelpers"/> already routes
/// the need to the right backing energy:
///   • Thanatic / Grav — the need's CurLevel is postfixed to the hediff's Energy.
///   • Basic VREA reactor — native CurLevel is VREA's own reactor energy.
///   • Vanometric — disables the need entirely, so TryGetNeed returns null and we
///     fall back to full (1.0): unlimited power = full glow.
///
/// No throttling / rebaking is involved. The mote path already rewrites
/// instanceColor every tick; the render-node path scales the per-draw
/// MaterialPropertyBlock alpha — both are allocation-free reads of the live need
/// value, so a slow power drain costs nothing.
/// </summary>
public static class ReactorGlow
{
    // Glow alpha at full charge (the value scaled down by power fraction). 1.0
    // matches the historical fixed opacity, so the setting toggles cleanly between
    // "fade with power" and exactly the previous behaviour.
    public const float PeakOpacity = 1f;

    private static NeedDef _needDef;
    private static bool _needResolved;
    private static NeedDef ReactorNeedDef
    {
        get
        {
            if (!_needResolved)
            {
                _needResolved = true;
                _needDef = DefDatabase<NeedDef>.GetNamed("VREA_ReactorPower", errorOnFail: false);
            }
            return _needDef;
        }
    }

    /// <summary>
    /// The android's power-need fraction in [0, 1]. Returns 1f when the reactor
    /// need is absent (Vanometric suppresses it; a pawn with no reactor has none).
    /// </summary>
    public static float PowerFraction(Pawn pawn)
    {
        var need = (ReactorNeedDef != null) ? pawn?.needs?.TryGetNeed(ReactorNeedDef) : null;
        return need == null ? 1f : Mathf.Clamp01(need.CurLevelPercentage);
    }

    /// <summary>
    /// Glow alpha for this pawn: <see cref="PeakOpacity"/> scaled by the power
    /// fraction when the setting is enabled, otherwise a flat <see cref="PeakOpacity"/>.
    /// </summary>
    public static float GlowOpacity(Pawn pawn)
    {
        bool scale = ArchotechAndroidHardwareMod.Settings?.scaleReactorGlowByPower ?? true;
        return PeakOpacity * (scale ? PowerFraction(pawn) : 1f);
    }
}
