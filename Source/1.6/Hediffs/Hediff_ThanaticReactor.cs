using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Core hediff for the Thanatic Reactor. Unlike the Vanometric Reactor (which
/// disables the power need entirely), this reactor keeps VREA's power need
/// visible and drains faster than baseline. Humanlike kills refill it; overflow
/// spills into the <c>AAH_ThanaticOvercharge</c> combat buff; hitting zero
/// energy kills the pawn and ejects the reactor partially recharged.
///
/// Why not subclass VREA's Hediff_AndroidReactor: the mod stays reflection-only
/// (no VREAndroids.dll compile-time dep). We reimplement the ~10 lines of drain
/// math and read the PowerEfficiency curve from VREA via reflection at runtime.
///
/// Why Hediff_AddedPart base (same as Vanometric): this is a body-part
/// replacement in the reactor slot, not a brain implant. VREA's ShouldBeDowned
/// prefix force-downs pawns whose reactor isn't <c>Hediff_AndroidReactor</c> —
/// the existing PawnHealthTracker_ShouldBeDowned postfix handles that.
///
/// Power need wiring: VREA's <c>Need_ReactorPower.CurLevel</c> looks up the
/// reactor hediff by def (VREA_Reactor). With our hediff installed instead,
/// that lookup fails and the need reads 0. The NeedReactorPower_CurLevel patch
/// falls through to this hediff's Energy when VREA_Reactor is absent.
///
/// Drain rate: driven entirely by the biostatMet sum of the pawn's active
/// genes, piped through VREA's PowerEfficiencyToPowerDrainFactorCurve (same
/// mechanism VREA's own reactor uses). Our companion gene
/// <c>AAH_ThanaticReactor</c> contributes biostatMet -4, which accelerates
/// drain; the exact multiplier is whatever VREA's curve evaluates to at that
/// point (we don't assert a specific number here, and the gene's biostatMet
/// is the only lever we tweak to tune it).
///
/// Companion gene lifecycle: mirrors Hediff_VanometricReactor — add on install,
/// remove on uninstall, re-assert on post-load-init in case external mods
/// stripped it.
/// </summary>
public class Hediff_ThanaticReactor : Hediff_AddedPart, ICustomAAHEjection
{
    private float curEnergy = 1f;
    private bool dying;

    private static GeneDef _thanaticGene;
    private static GeneDef ThanaticGene =>
        _thanaticGene ??= DefDatabase<GeneDef>.GetNamed("AAH_ThanaticReactor", errorOnFail: false);

    private static HediffDef OverchargeDef =>
        _overchargeDefCache ??= DefDatabase<HediffDef>.GetNamed("AAH_ThanaticOvercharge", errorOnFail: false);

    private static DamageDef DepletionDamageDef =>
        _depletionDamageDefCache ??= DefDatabase<DamageDef>.GetNamed("AAH_ThanaticDepletion", errorOnFail: false);

    private static RulePackDef DrainEventRulePack =>
        _drainEventRulePackCache ??= DefDatabase<RulePackDef>.GetNamed("AAH_Event_ThanaticDrain", errorOnFail: false);

    private static HediffDef DepletionCulpritDef =>
        _depletionCulpritDefCache ??= DefDatabase<HediffDef>.GetNamed("AAH_ThanaticDepletionCulprit", errorOnFail: false);

    private static HediffDef EjectionInjuryDef =>
        _ejectionInjuryDefCache ??= DefDatabase<HediffDef>.GetNamed("AAH_ReactorEjectionInjury", errorOnFail: false);

    // Transient dessication queue: Notify_KilledPawn fires inside Pawn.Kill
    // before the victim's Corpse is spawned, so we poll for the Corpse in
    // subsequent TickIntervals. Not serialized — any pending dessications are
    // dropped on save/load (rare edge case; kills and saves rarely align).
    private readonly List<Pawn> _pendingDessication = new();
    private readonly List<int> _pendingDessicationTicksWaited = new();
    private const int MaxDessicationWaitTicks = 300;
    private const float DessicationRotProgressTarget = 1_000_000f;

    private static SimpleCurve _drainCurveCache;
    private static bool _drainCurveResolved;
    private static ThingDef _thingDefCache;
    private static HediffDef _overchargeDefCache;
    private static DamageDef _depletionDamageDefCache;
    private static RulePackDef _drainEventRulePackCache;
    private static HediffDef _depletionCulpritDefCache;
    private static HediffDef _ejectionInjuryDefCache;

    public float Energy
    {
        get => curEnergy;
        set => curEnergy = Mathf.Clamp01(value);
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
        AddGeneIfMissing();
    }

    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (pawn == null || pawn.Dead || dying) return;
        if (!Gen.IsHashIntervalTick(pawn, 60, delta)) return;

        DrainEnergy();
        ProcessDessicationQueue();

        if (curEnergy <= 0f && !pawn.Dead)
            TriggerDeath();
    }

    public override void Notify_KilledPawn(Pawn victim, DamageInfo? dinfo)
    {
        base.Notify_KilledPawn(victim, dinfo);
        if (pawn == null || pawn.Dead || dying) return;
        if (victim == null || victim == pawn) return;
        if (victim.RaceProps?.Humanlike != true) return;

        var settings = ArchotechAndroidHardwareMod.Settings;
        if (settings == null) return;

        float refill = settings.thanaticRefillAmount;
        float before = curEnergy;
        float after = before + refill;
        float overflow = after > 1f ? after - 1f : 0f;
        curEnergy = Mathf.Min(1f, after);

        if (overflow > 0f)
            Hediff_ThanaticOvercharge.ApplyOrExtend(pawn, overflow, settings);

        if (victim.Map != null)
        {
            _pendingDessication.Add(victim);
            _pendingDessicationTicksWaited.Add(0);
        }

        LogDrainEvent(victim);
    }

    public void EjectCustom(Pawn pawn, IntVec3 position, Map map)
    {
        SpawnReactorItem(position, map, curEnergy);
    }

    public override void PostRemoved()
    {
        var bodyPart = Part;
        base.PostRemoved();
        RemoveGeneIfPresent();

        // Destroy the reactor slot on standalone removal (matches Vanometric
        // Reactor behavior). SuppressBodyPartDestruction is set by the install
        // patch during replacement surgery, where the slot must stay intact.
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
        Scribe_Values.Look(ref dying, "dying");
        // Invariant: this hediff requires its companion gene to drive the drain
        // rate via biostatMet. Re-assert presence after load in case an external
        // mod has stripped it.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            AddGeneIfMissing();
    }

    private void AddGeneIfMissing()
    {
        if (pawn?.genes == null || ThanaticGene == null) return;
        if (pawn.genes.GenesListForReading.Any(g => g.def == ThanaticGene)) return;
        pawn.genes.AddGene(ThanaticGene, xenogene: true);
    }

    private void RemoveGeneIfPresent()
    {
        if (pawn?.genes == null || ThanaticGene == null) return;
        var gene = pawn.genes.GenesListForReading.FirstOrDefault(g => g.def == ThanaticGene);
        if (gene != null)
            pawn.genes.RemoveGene(gene);
    }

    private void DrainEnergy()
    {
        // Baseline VREA drain formula. Acceleration relative to VREA's own
        // reactor comes from the AAH_ThanaticReactor gene's biostatMet -4
        // feeding into PowerEfficiencyDrainMultiplier (whose output is VREA's
        // curve, not ours) — no separate multiplier applied here.
        float drainPerCheck = 1.388889e-7f * PowerEfficiencyDrainMultiplier() * 60f;
        curEnergy = Mathf.Max(0f, curEnergy - drainPerCheck);
    }

    private void ProcessDessicationQueue()
    {
        for (int i = _pendingDessication.Count - 1; i >= 0; i--)
        {
            var victim = _pendingDessication[i];
            _pendingDessicationTicksWaited[i] += 60;

            if (victim?.Corpse != null && !victim.Corpse.Destroyed)
            {
                DessicateCorpse(victim.Corpse);
                _pendingDessication.RemoveAt(i);
                _pendingDessicationTicksWaited.RemoveAt(i);
            }
            else if (_pendingDessicationTicksWaited[i] > MaxDessicationWaitTicks)
            {
                // Gave up — corpse never materialised (despawned, destroyed,
                // or some other edge case).
                _pendingDessication.RemoveAt(i);
                _pendingDessicationTicksWaited.RemoveAt(i);
            }
        }
    }

    private void TriggerDeath()
    {
        dying = true;
        var settings = ArchotechAndroidHardwareMod.Settings;
        float refillOnDeath = settings?.thanaticRefillAmount ?? 0.35f;

        // Snapshot before Kill — we'll reference these against the corpse's
        // inner pawn during post-Kill cleanup.
        var deathPawn = pawn;
        var position = deathPawn.PositionHeld;
        var map = deathPawn.MapHeld;
        var bodyPart = Part;

        // Lore: the pawn's own death feeds the reactor one last time. Spawn
        // before Kill so the item position matches the pawn's live position
        // rather than the corpse's ending position.
        SpawnReactorItem(position, map, refillOnDeath);

        // Death letter: DamageDef.deathMessage via DamageInfo takes priority
        // over the culprit-hediff fallback in HealthUtility.GetDiedLetterText.
        DamageInfo? dinfo = DepletionDamageDef != null
            ? new DamageInfo(DepletionDamageDef, 0f)
            : (DamageInfo?)null;

        // Combat log death entry: Pawn.Kill calls BattleLogEntry_StateTransition
        // with exactCulprit → CULPRITHEDIFF_labelNoun in the rule pack. A
        // floating Hediff (never added to the pawn) is sufficient; the entry
        // only reads .def and .Part.
        Hediff culprit = DepletionCulpritDef != null
            ? HediffMaker.MakeHediff(DepletionCulpritDef, deathPawn)
            : null;

        // Kill before cleaning up hediffs. Doing it in the other order would
        // add MissingBodyPart (via PostRemoved) on a live pawn, which VREA's
        // no-reactor ShouldBeDowned rule then turns into a downed-transition
        // battle log entry ("missing a stomach made X crumple") before the
        // die-transition entry fires. Once pawn.Dead is true, CheckForStateChange
        // short-circuits and post-Kill edits don't emit phantom transitions.
        deathPawn.Kill(dinfo, culprit);

        CleanUpCorpseHediffs(deathPawn, bodyPart);
    }

    private void CleanUpCorpseHediffs(Pawn deathPawn, BodyPartRecord bodyPart)
    {
        var overchargeHediff = OverchargeDef != null
            ? deathPawn.health.hediffSet.GetFirstHediffOfDef(OverchargeDef)
            : null;
        if (overchargeHediff != null)
            deathPawn.health.RemoveHediff(overchargeHediff);

        // Suppress the default MissingBodyPart installation inside our own
        // PostRemoved so we can add it below with an explicit lastInjury
        // (gives the corpse an "ejected" label instead of the Inside-part
        // default "destroyed").
        var prevSuppress = SurgeryState.SuppressBodyPartDestruction;
        SurgeryState.SuppressBodyPartDestruction = true;
        try
        {
            deathPawn.health.RemoveHediff(this);
        }
        finally
        {
            SurgeryState.SuppressBodyPartDestruction = prevSuppress;
        }

        if (bodyPart == null) return;
        var missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, deathPawn, bodyPart);
        // Hediff_MissingPart.LabelBase picks destroyedLabel when
        // alwaysUseDestroyedLabel is set on the lastInjury's injuryProps —
        // the only route to a custom label on Inside body parts.
        if (EjectionInjuryDef != null)
            missing.lastInjury = EjectionInjuryDef;
        deathPawn.health.AddHediff(missing, bodyPart);
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

    private void LogDrainEvent(Pawn victim)
    {
        // Adds a victim-perspective entry to the combat log so players can
        // trace the corpse's immediate dessication back to the reactor rather
        // than mistaking it for unrelated rot acceleration.
        var rulePack = DrainEventRulePack;
        if (rulePack == null || pawn == null) return;
        Find.BattleLog.Add(new BattleLogEntry_Event(victim, rulePack, pawn));
    }

    private static void DessicateCorpse(Corpse corpse)
    {
        if (corpse == null || corpse.Destroyed) return;
        var rottable = corpse.TryGetComp<CompRottable>();
        if (rottable == null) return;
        // Overshoot the dessicated threshold decisively — vanilla's thresholds
        // vary by corpse and mod config; ~33 game-days of rot progress safely
        // clears any reasonable configuration.
        if (rottable.RotProgress < DessicationRotProgressTarget)
            rottable.RotProgress = DessicationRotProgressTarget;
    }

    private static void SpawnReactorItem(IntVec3 pos, Map map, float energy)
    {
        if (map == null) return;
        var thingDef = _thingDefCache ??= DefDatabase<ThingDef>.GetNamed("AAH_ThanaticReactor", errorOnFail: false);
        if (thingDef == null) return;
        var thing = ThingMaker.MakeThing(thingDef);
        if (thing is ThanaticReactorThing tr)
            tr.storedEnergy = Mathf.Clamp01(energy);
        GenPlace.TryPlaceThing(thing, pos, map, ThingPlaceMode.Near);
    }
}
