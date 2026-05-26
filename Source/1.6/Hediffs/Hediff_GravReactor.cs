using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Core hediff for the grav reactor. Drains at VREA's baseline rate (driven by
/// the companion gene's biostatMet 0 through VREA's
/// PowerEfficiencyToPowerDrainFactorCurve) and is refilled in full whenever the
/// host participates in a gravship launch ritual — see
/// <see cref="VREAPatches.RitualOutcomeWorker_GravshipLaunch_Apply_Patch"/>.
///
/// On depletion: the hediff's Severity tracks (1 - Energy), so when Energy
/// hits zero the empty stage activates and clamps Consciousness to 0. The
/// PawnHealthTracker_ShouldBeDowned postfix then re-evaluates capacities and
/// forces the pawn downed. The pawn does NOT die — unlike Thanatic, recharging
/// (via a launch) or replacing the reactor revives them.
///
/// Companion gene lifecycle: mirrors Hediff_VanometricReactor / Hediff_ThanaticReactor —
/// add on install, remove on uninstall, re-assert on post-load-init in case
/// external mods stripped it.
///
/// Why Hediff_AddedPart base: body-part replacement in the reactor slot. VREA's
/// ShouldBeDowned prefix would force-down the pawn permanently because we're
/// not a Hediff_AndroidReactor; PawnHealthTracker_ShouldBeDowned_Postfix
/// handles that fallthrough (this hediff's def is in its allowlist).
///
/// Power need wiring: VREA's Need_ReactorPower.CurLevel looks up by def name
/// (VREA_Reactor). The NeedReactorPower_CurLevel patches fall through to any
/// hediff implementing <see cref="IAAHReactorEnergy"/> when VREA_Reactor is
/// absent — both getter and setter route through this hediff's Energy.
///
/// State transfer: implements <see cref="ICustomAAHEjection"/> so replacement
/// surgery preserves the current Energy onto the freshly-spawned reactor
/// item, matching the Thanatic pattern. Install transfer is handled by
/// <see cref="VREAPatches.GravReactorInstallEnergyTransfer_Patch"/>.
/// </summary>
public class Hediff_GravReactor : Hediff_AddedPart, ICustomAAHEjection, IAAHReactorEnergy
{
    private float curEnergy = 1f;

    // Cyan-blue glow exactly matching the chest body attachment's core RGB
    // (0, 182, 239 in 8-bit). Transient — recreated on the first post-load
    // tick by ReactorGlowMote.
    private static readonly Color GlowTint = new(0f, 0.714f, 0.937f);
    private Mote glowMote;

    // Pending charge-aura spawn: armed by Notify_RechargedByLaunch when a
    // gravship landing refills this reactor, fired by the next Tick once the
    // host pawn is settled on the destination map. Deferred (rather than
    // spawned inline at the landing-postfix callsite) because pawns aren't
    // guaranteed to be Spawned at that moment — the gravship machinery
    // places them into the destination map as part of the landing sequence,
    // not before InitiateLanding returns. Negative = inactive. Counts down
    // each tick as a safety net so a never-spawning pawn doesn't leak the
    // flag across the rest of the save.
    private int chargeAuraPendingTicks = -1;
    private const int ChargeAuraPendingTimeoutTicks = 600;   // 10s safety net

    // Scroll magnitudes mirror Hediff_ThanaticReactor's source aura: cloud
    // layers at 0.15 (matches vanilla texA/texB intensity), speckle layer
    // at 0.5 (matches the MoteMultiplyAddScroll shader's compiled-in
    // _DetailScrollSpeed default; the vanilla XML lowercase override is a
    // no-op — see Graphic_PawnBodySilhouette_DrawWorker_Patch).
    private const float CloudScrollSpeed = 0.15f;
    private const float DetailScrollSpeed = 0.5f;
    private static readonly Vector2 ChargeWorldNorth = new(0f, 1f);

    private static GeneDef _gravGene;
    private static GeneDef GravGene =>
        _gravGene ??= DefDatabase<GeneDef>.GetNamed("AAH_GravReactor", errorOnFail: false);

    private static ThingDef _thingDefCache;

    private static ThingDef _chargeAuraMoteDefCache;
    private static ThingDef ChargeAuraMoteDef =>
        _chargeAuraMoteDefCache ??= DefDatabase<ThingDef>.GetNamed("AAH_GravChargeAura", errorOnFail: false);

    private static SimpleCurve _drainCurveCache;
    private static bool _drainCurveResolved;

    public float Energy
    {
        get => curEnergy;
        set
        {
            curEnergy = Mathf.Clamp01(value);
            SyncSeverityToEnergy();
        }
    }

    public override bool ShouldRemove => false;

    public override void PostAdd(DamageInfo? dinfo)
    {
        base.PostAdd(dinfo);
        // Default to full when no explicit energy was set by the install recipe
        // patch (e.g., dev-spawned hediffs). The install patch overwrites this
        // with the ingredient item's storedEnergy.
        if (curEnergy <= 0f)
            curEnergy = 1f;
        SyncSeverityToEnergy();
        AddGeneIfMissing();
    }

    // Tick (not TickInterval): the mote maintenance cadence must beat the
    // mote's solidTime=600 even when the pawn is offscreen and Thing.DoTick
    // throttles TickInterval. See ReactorGlowMote / Hediff_VanometricReactor
    // for the full rationale.
    public override void Tick()
    {
        base.Tick();
        if (pawn == null || pawn.Dead) return;
        ReactorGlowMote.Maintain(pawn, ref glowMote, GlowTint, brightness: 1f);
        TickChargeAuraPending();
    }

    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (pawn == null || pawn.Dead) return;
        if (!Gen.IsHashIntervalTick(pawn, 60, delta)) return;

        DrainEnergy();
        SyncSeverityToEnergy();
    }

    public void EjectCustom(Pawn pawn, IntVec3 position, Map map)
    {
        SpawnReactorItem(position, map, curEnergy);
    }

    /// <summary>
    /// Recharge entry point for the gravship-landing patch. Refills the reactor
    /// and arms the cyan charging aura, which fires on the next Tick once the
    /// host pawn is back on a map. Coalescing two landings within the same
    /// ~10s window is fine — the second call just resets the pending timer to
    /// the full window; the aura plays once when the pawn next ticks-while-spawned.
    /// </summary>
    public void Notify_RechargedByLaunch()
    {
        Energy = 1f;
        chargeAuraPendingTicks = ChargeAuraPendingTimeoutTicks;
    }

    public override void PostRemoved()
    {
        var bodyPart = Part;
        base.PostRemoved();
        RemoveGeneIfPresent();

        // Destroy the reactor slot on standalone removal (matches Vanometric /
        // Thanatic). SuppressBodyPartDestruction is set by the install patch
        // during replacement surgery where the slot must stay intact.
        if (!SurgeryState.SuppressBodyPartDestruction && pawn?.health?.hediffSet != null && bodyPart != null)
        {
            pawn.health.AddHediff(HediffDefOf.MissingBodyPart, bodyPart);
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref curEnergy, "curEnergy", 1f);
        Scribe_Values.Look(ref chargeAuraPendingTicks, "chargeAuraPendingTicks", -1);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            SyncSeverityToEnergy();
            AddGeneIfMissing();
        }
    }

    private void AddGeneIfMissing()
    {
        if (pawn?.genes == null || GravGene == null) return;
        if (pawn.genes.GenesListForReading.Any(g => g.def == GravGene)) return;
        pawn.genes.AddGene(GravGene, xenogene: true);
    }

    private void RemoveGeneIfPresent()
    {
        if (pawn?.genes == null || GravGene == null) return;
        var gene = pawn.genes.GenesListForReading.FirstOrDefault(g => g.def == GravGene);
        if (gene != null)
            pawn.genes.RemoveGene(gene);
    }

    private void SyncSeverityToEnergy()
    {
        // Severity is binary — 1 when fully depleted (selects the empty stage
        // whose Consciousness=0 capMod forces the pawn downed), 0 otherwise.
        // Bool-style (rather than Severity = 1 - Energy) so the Hediff.Severity
        // setter — which calls Notify_HediffChanged → DirtyCache → invalidates
        // the capacity cache — only fires on the 0↔depleted crossing, not on
        // every drain tick.
        float target = curEnergy <= 0f ? 1f : 0f;
        if (!Mathf.Approximately(Severity, target))
            Severity = target;
    }

    private void TickChargeAuraPending()
    {
        if (chargeAuraPendingTicks < 0) return;
        if (pawn != null && pawn.Spawned && pawn.MapHeld != null)
        {
            SpawnChargeAura(pawn);
            chargeAuraPendingTicks = -1;
            return;
        }
        chargeAuraPendingTicks--;
        if (chargeAuraPendingTicks <= 0)
            chargeAuraPendingTicks = -1;
    }

    // Powering-up aura: all three layers flow world-north, reading as "reactor
    // charging up". Parallels Hediff_ThanaticReactor.SpawnDirectionalSourceAura,
    // just with the cyan-blue grav mote def and no kill-stream coordination.
    // Visual flow is opposite to the scroll vector (MoteMultiplyAddScroll
    // samples in world space and pattern motion is sample-drift inverted), so
    // to make the pattern visually flow world-north we set scroll = -north * s.
    private static void SpawnChargeAura(Pawn target)
    {
        var moteDef = ChargeAuraMoteDef;
        if (moteDef == null || target == null) return;
        if (!target.Spawned || target.MapHeld == null) return;

        var mote = (Mote_ThanaticSilhouetteAura)ThingMaker.MakeThing(moteDef);
        mote.exactPosition = target.DrawPos;
        mote.Attach(target);
        mote.texAScroll = -ChargeWorldNorth * CloudScrollSpeed;
        mote.texBScroll = -ChargeWorldNorth * CloudScrollSpeed;
        mote.detailScroll = -ChargeWorldNorth * DetailScrollSpeed;
        mote.overrideScroll = true;
        GenSpawn.Spawn(mote, target.PositionHeld, target.MapHeld);
    }

    private void DrainEnergy()
    {
        // Baseline VREA drain formula — same constant Thanatic uses, since the
        // tuning lever is entirely the companion gene's biostatMet feeding
        // VREA's curve. The grav reactor's gene contributes biostatMet 0, so
        // the realised rate is whatever VREA's curve evaluates to at the
        // pawn's summed metabolism (typically 1.0× — baseline).
        float drainPerCheck = 1.388889e-7f * PowerEfficiencyDrainMultiplier() * 60f;
        curEnergy = Mathf.Max(0f, curEnergy - drainPerCheck);
    }

    private float PowerEfficiencyDrainMultiplier()
    {
        var curve = DrainCurve();
        if (curve == null || pawn?.genes == null) return 1f;

        int metSum = 0;
        foreach (var gene in pawn.genes.GenesListForReading)
        {
            if (!gene.Overridden)
                metSum += gene.def.biostatMet;
        }
        return curve.Evaluate(metSum);
    }

    private static SimpleCurve DrainCurve()
    {
        if (_drainCurveResolved) return _drainCurveCache;
        _drainCurveResolved = true;
        var type = AccessTools.TypeByName("VREAndroids.AndroidStatsTable");
        if (type == null) return null;
        _drainCurveCache = AccessTools.Field(type, "PowerEfficiencyToPowerDrainFactorCurve")?.GetValue(null) as SimpleCurve;
        return _drainCurveCache;
    }

    private static void SpawnReactorItem(IntVec3 pos, Map map, float energy)
    {
        if (map == null) return;
        var thingDef = _thingDefCache ??= DefDatabase<ThingDef>.GetNamed("AAH_GravReactor", errorOnFail: false);
        if (thingDef == null) return;
        var thing = ThingMaker.MakeThing(thingDef);
        if (thing is GravReactorThing gr)
            gr.storedEnergy = Mathf.Clamp01(energy);
        GenPlace.TryPlaceThing(thing, pos, map, ThingPlaceMode.Near);
    }
}
