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
public class Hediff_ThanaticReactor : Hediff_AddedPart, ICustomAAHEjection, IAAHReactorEnergy
{
    private float curEnergy = 1f;
    private bool dying;

    // Red glow mirroring the colour the previous FireGlow render nodes used.
    // Transient — recreated on the first post-load tick by ReactorGlowMote.
    private static readonly Color GlowTint = new(1f, 0.25f, 0.2f);
    private Mote glowMote;

    // Ticks remaining until the delayed source-pawn aura fires after a kill.
    // Negative = inactive. First-kill-wins: subsequent kills within the window
    // don't reset the timer (see Notify_KilledPawn).
    private int sourceAuraRemainingTicks = -1;

    // Ticks remaining in the drain-death countdown. Energy has reached zero;
    // the death aura is playing; ExecuteDeath fires when this hits zero.
    // Negative = not in dying state (redundant with `dying` but avoids relying
    // on a single bool for countdown control-flow).
    private int dyingTicksRemaining = -1;

    private const int SourceAuraDelayTicks = 60;   // 1.0s
    private const int DyingCountdownTicks = 300;    // 5.0s

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

    private static ThingDef AuraShortMoteDef =>
        _auraShortMoteDefCache ??= DefDatabase<ThingDef>.GetNamed("AAH_ThanaticAura", errorOnFail: false);

    private static ThingDef AuraLongMoteDef =>
        _auraLongMoteDefCache ??= DefDatabase<ThingDef>.GetNamed("AAH_ThanaticAuraLong", errorOnFail: false);

    private static ThingDef StreamControllerDef =>
        _streamControllerDefCache ??= DefDatabase<ThingDef>.GetNamed("AAH_ThanaticStreamController", errorOnFail: false);

    private static DesignationDef ExtractDesignationDef =>
        _extractDesignationDefCache ??= DefDatabase<DesignationDef>.GetNamed("AAH_ExtractThanaticReactor", errorOnFail: false);

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
    private static ThingDef _auraShortMoteDefCache;
    private static ThingDef _auraLongMoteDefCache;
    private static ThingDef _streamControllerDefCache;
    private static DesignationDef _extractDesignationDefCache;

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

    // Tick (not TickInterval): in 1.6 Thing.DoTick batches TickInterval at
    // UpdateRateTicks cadence, which can exceed the mote's solidTime=600 for
    // offscreen pawns and cause the mote to despawn between maintenance calls.
    // Hediff.Tick runs every game tick regardless of distance — same cadence
    // noctol's CompTick uses for its eye glow. The drain math stays in
    // TickInterval because it legitimately wants delta-batching.
    public override void Tick()
    {
        base.Tick();
        if (pawn == null || pawn.Dead) return;
        ReactorGlowMote.Maintain(pawn, ref glowMote, GlowTint, brightness: 1f);
    }

    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (pawn == null || pawn.Dead) return;

        // FX timers respond to arbitrary delta granularity so the 1-second
        // source-aura offset isn't quantized to the 60-tick drain cadence.
        TickSourceAuraDelay(delta);

        if (dying)
        {
            TickDyingCountdown(delta);
            // Drain and kill checks are frozen during the death countdown, but
            // any prior-kill dessication polling should still wind down.
            if (Gen.IsHashIntervalTick(pawn, 60, delta))
                ProcessDessicationQueue();
            return;
        }

        if (!Gen.IsHashIntervalTick(pawn, 60, delta)) return;

        DrainEnergy();
        ProcessDessicationQueue();

        if (curEnergy <= 0f)
            EnterDyingState();
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

        SpawnDirectionalVictimAura(victim, pawn);
        SpawnStreamController(pawn, victim);

        // First-kill-wins: if another kill arrives before the source aura has
        // fired, don't reset the countdown — otherwise chain-kills could
        // indefinitely defer the source aura.
        if (sourceAuraRemainingTicks < 0)
            sourceAuraRemainingTicks = SourceAuraDelayTicks;
    }

    public void EjectCustom(Pawn pawn, IntVec3 position, Map map)
    {
        SpawnReactorItem(position, map, curEnergy);
    }

    /// <summary>
    /// Player-driven extraction from an android corpse. Mirrors the post-death
    /// flow in <see cref="ExecuteDeath"/>: spawns the reactor item carrying the
    /// hediff's current energy, then removes the hediff with the same
    /// "ejected" missing-body-part labeling. Called from
    /// <see cref="JobDriver_ExtractThanaticReactor"/>.
    /// </summary>
    public void ExtractFromCorpse(Corpse corpse)
    {
        if (corpse == null || corpse.Destroyed) return;
        var innerPawn = corpse.InnerPawn;
        if (innerPawn == null) return;
        var map = corpse.MapHeld;
        if (map == null) return;

        var bodyPart = Part;
        SpawnReactorItem(corpse.PositionHeld, map, curEnergy);
        CleanUpCorpseHediffs(innerPawn, bodyPart);
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        var baseGizmos = base.GetGizmos();
        if (baseGizmos != null)
            foreach (var g in baseGizmos) yield return g;

        if (pawn == null || !pawn.Dead) yield break;
        var corpse = pawn.Corpse;
        if (corpse == null || !corpse.Spawned || corpse.Destroyed) yield break;
        var map = corpse.Map;
        if (map == null) yield break;
        var designationDef = ExtractDesignationDef;
        if (designationDef == null) yield break;

        var existing = map.designationManager.DesignationOn(corpse, designationDef);
        if (existing == null)
        {
            yield return new Command_Action
            {
                defaultLabel = "Extract thanatic reactor",
                defaultDesc = "Mark this android corpse to have its thanatic reactor extracted. The reactor item will be recovered with its current charge intact.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/AAH_ExtractThanaticReactor"),
                action = delegate
                {
                    if (map.designationManager.DesignationOn(corpse, designationDef) == null)
                        map.designationManager.AddDesignation(new Designation(corpse, designationDef));
                }
            };
        }
        else
        {
            yield return new Command_Action
            {
                defaultLabel = "Cancel reactor extraction",
                defaultDesc = "Remove the thanatic reactor extraction designation from this corpse.",
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                action = delegate
                {
                    var d = map.designationManager.DesignationOn(corpse, designationDef);
                    if (d != null) map.designationManager.RemoveDesignation(d);
                }
            };
        }
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
        Scribe_Values.Look(ref sourceAuraRemainingTicks, "sourceAuraRemainingTicks", -1);
        Scribe_Values.Look(ref dyingTicksRemaining, "dyingTicksRemaining", -1);
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

    private void TickSourceAuraDelay(int delta)
    {
        if (sourceAuraRemainingTicks < 0) return;
        sourceAuraRemainingTicks -= delta;
        if (sourceAuraRemainingTicks > 0) return;
        sourceAuraRemainingTicks = -1;
        if (pawn != null && !pawn.Dead && pawn.Spawned)
            SpawnDirectionalSourceAura(pawn);
    }

    private void TickDyingCountdown(int delta)
    {
        if (dyingTicksRemaining <= 0) return;
        dyingTicksRemaining -= delta;
        if (dyingTicksRemaining <= 0)
            ExecuteDeath();
    }

    // MoteMultiplyAddScroll samples in world space (via _pawnCenterWorld),
    // so scroll-speed vectors are world-space directions, not UV-space —
    // mesh rotation doesn't factor in. Visual pattern motion is opposite to
    // the scroll vector (sample drift +x → pattern appears to move -x), so
    // to produce visual flow in world direction `W` we set scroll = -W*speed.
    //
    // Magnitudes: cloud layers get 0.15 (matches the vanilla texA/texB XML,
    // which is the only part of Mote_ResurrectAbility that actually reaches
    // the shader). Speckle layer gets 0.5 to match the shader's compiled-in
    // _DetailScrollSpeed default magnitude — the vanilla XML override for
    // this param is a no-op (wrong case; see DrawWorker patch), so the
    // shader's own (0.5, 0.5) default is what vanilla speckles actually run
    // at, and we match that intensity.
    private const float CloudScrollSpeed = 0.15f;
    private const float DetailScrollSpeed = 0.5f;

    private static readonly Vector2 WorldNorth = new(0f, 1f);
    private static readonly Vector2 WorldSouth = new(0f, -1f);

    // Victim aura: clouds rise as smoke, speckles peel off toward the killer.
    // The speckle direction is baked to kill-time geometry — if the android
    // moves during the 2s aura window, the speckles keep pointing at where
    // the android was at moment-of-kill, which reads as "the drain was
    // locked in at that instant".
    private static void SpawnDirectionalVictimAura(Pawn victim, Pawn source)
    {
        if (victim == null || source == null) return;
        var toSource = source.DrawPos - victim.DrawPos;
        var toSourceDir = new Vector2(toSource.x, toSource.z);
        if (toSourceDir.sqrMagnitude < 1e-6f)
            toSourceDir = WorldNorth;
        toSourceDir.Normalize();

        SpawnDirectionalAura(AuraShortMoteDef, victim,
            texAScroll: -WorldNorth * CloudScrollSpeed,
            texBScroll: -WorldNorth * CloudScrollSpeed,
            detailScroll: -toSourceDir * DetailScrollSpeed);
    }

    // Source aura: all three layers flow world-north, reinforcing the "reactor
    // is being powered up" read. Uniform upward motion contrasts with the
    // victim's speckles-pulled-sideways directionality.
    private static void SpawnDirectionalSourceAura(Pawn source)
    {
        SpawnDirectionalAura(AuraShortMoteDef, source,
            texAScroll: -WorldNorth * CloudScrollSpeed,
            texBScroll: -WorldNorth * CloudScrollSpeed,
            detailScroll: -WorldNorth * DetailScrollSpeed);
    }

    // Dying aura: all three layers flow world-south, the inverse of the
    // source powered-up aura. Reads as "reactor draining to nothing" — the
    // downward smoke and speckle motion mirrors the upward source aura so
    // the two moments read as narrative opposites.
    private static void SpawnDirectionalDyingAura(Pawn pawn)
    {
        SpawnDirectionalAura(AuraLongMoteDef, pawn,
            texAScroll: -WorldSouth * CloudScrollSpeed,
            texBScroll: -WorldSouth * CloudScrollSpeed,
            detailScroll: -WorldSouth * DetailScrollSpeed);
    }

    private static void SpawnDirectionalAura(ThingDef moteDef, Pawn target,
        Vector2 texAScroll, Vector2 texBScroll, Vector2 detailScroll)
    {
        if (moteDef == null || target == null) return;
        if (!target.Spawned || target.MapHeld == null) return;

        var mote = (Mote_ThanaticSilhouetteAura)ThingMaker.MakeThing(moteDef);
        mote.exactPosition = target.DrawPos;
        mote.Attach(target);
        mote.texAScroll = texAScroll;
        mote.texBScroll = texBScroll;
        mote.detailScroll = detailScroll;
        mote.overrideScroll = true;
        GenSpawn.Spawn(mote, target.PositionHeld, target.MapHeld);
    }

    private static void SpawnStreamController(Pawn source, Pawn victim)
    {
        if (StreamControllerDef == null || source == null || victim == null) return;
        var map = victim.MapHeld;
        if (map == null) return;
        var controller = (ThanaticStreamController)ThingMaker.MakeThing(StreamControllerDef);
        controller.sourcePawn = source;
        controller.victim = victim;
        GenSpawn.Spawn(controller, victim.PositionHeld, map);
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

    // Enter the dramatized death countdown. The red aura starts playing on the
    // still-alive pawn; ExecuteDeath runs the actual Kill sequence once the
    // countdown expires. Splitting the phases this way means the aura attaches
    // cleanly to the live Pawn (no corpse-shift concerns) and the pawn stays
    // upright through the full window because PawnHealthTracker_ShouldBeDowned
    // already keeps AAH reactor pawns standing regardless of energy level.
    private void EnterDyingState()
    {
        dying = true;
        dyingTicksRemaining = DyingCountdownTicks;
        SpawnDirectionalDyingAura(pawn);
    }

    private void ExecuteDeath()
    {
        dyingTicksRemaining = 0;
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
