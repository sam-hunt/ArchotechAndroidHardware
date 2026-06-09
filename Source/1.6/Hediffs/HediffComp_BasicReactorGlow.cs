using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Per-tick glow mote maintainer for VREA's stock reactor (VREA_Reactor),
/// attached to that HediffDef via XPath patch (see 1.6/Patches/
/// VREA_BasicReactor_AddVisuals.xml). Drives <see cref="ReactorGlowMote"/>
/// every game tick so plain androids visually match the AAH exotic variants
/// (Vanometric / Grav / Thanatic) — the chest body attachment alone reads
/// flat without the additive halo overlay.
///
/// Why a HediffComp rather than a Harmony patch on Hediff_AndroidReactor.Tick:
/// VREA's reactor hediff only overrides TickInterval (gated by UpdateRateTicks,
/// which throttles offscreen). The base Hediff.Tick lives on Verse.Hediff
/// itself — patching it would affect every hediff in the game. A comp hangs
/// off the def via XML, requires no reflection coupling, and its CompPostTick
/// fires every tick via HediffWithComps.PostTick → which is reached because
/// Hediff_AndroidReactor inherits HediffWithComps unchanged through
/// Hediff_AndroidPart → Hediff_AddedPart.
///
/// Tint: white at full brightness. The basic reactor is the "stock" variant;
/// reusing one of the coloured tints (lime / cyan / red) would imply functional
/// parity with that exotic. White reads as "powered, unmodified".
/// </summary>
public class HediffComp_BasicReactorGlow : HediffComp
{
    private Mote glowMote;

    public override void CompPostTick(ref float severityAdjustment)
    {
        ReactorGlowMote.Maintain(Pawn, parent.def, ref glowMote, Color.white);
    }
}
