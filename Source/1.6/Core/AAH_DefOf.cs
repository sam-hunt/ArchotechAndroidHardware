using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

// Centralised def references, mirroring vanilla's *DefOf and VREA's VREA_DefOf.
// Fields are bound by DefOfHelper during load (before [StaticConstructorOnStartup]),
// so a renamed/missing def surfaces as a one-time config error at startup instead of
// a silent null + broken feature at runtime — and a plain static field read is cheaper
// than a per-call DefDatabase<T>.GetNamed lookup.
//
// Why several classes instead of one AAH_DefOf: [DefOf] binds each field by
// "field name == defName", so field names must be unique. Our companion-part
// convention reuses one defName across types (AAH_ThanaticReactor / AAH_GravReactor
// each exist as a HediffDef, GeneDef AND ThingDef; AAH_ExtractThanaticReactor is both
// a DesignationDef and a JobDef). Every def type that participates in such a name clash
// gets its own type-scoped class; AAH_DefOf below is the catch-all for the rest.
//
// MayRequire: defs from optional content stay null (not a load error) when that content
// is absent — the grav reactor's def set is Odyssey-gated, and the VPE violence
// generator comes from Vanilla Expanded - Power. Call sites already null-check these.
// VREA defs (VREA_*) are plain fields: VREA is a hard modDependency, so failing loud if
// one goes missing (e.g. a VREA rename) is the desired behaviour.

[DefOf]
public static class AAH_HediffDefOf
{
    public static HediffDef AAH_ThanaticReactor;
    public static HediffDef AAH_VanometricReactor;
    public static HediffDef AAH_PsychicTransceiver;
    public static HediffDef AAH_ThanaticOvercharge;
    public static HediffDef AAH_ThanaticDepletionCulprit;
    public static HediffDef AAH_ReactorEjectionInjury;

    [MayRequire("Ludeon.RimWorld.Odyssey")]
    public static HediffDef AAH_GravReactor;
    [MayRequire("Ludeon.RimWorld.Odyssey")]
    public static HediffDef AAH_GravOvercharge;

    public static HediffDef VREA_Reactor;
    public static HediffDef VREA_NeutroLoss;

    static AAH_HediffDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(AAH_HediffDefOf));
    }
}

[DefOf]
public static class AAH_GeneDefOf
{
    public static GeneDef AAH_ThanaticReactor;
    public static GeneDef AAH_VanometricReactor;
    public static GeneDef AAH_PsychicTransceiver;
    public static GeneDef AAH_Neutrosynthesizer;
    public static GeneDef AAH_ArchotechMnemocore;

    [MayRequire("Ludeon.RimWorld.Odyssey")]
    public static GeneDef AAH_GravReactor;

    static AAH_GeneDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(AAH_GeneDefOf));
    }
}

[DefOf]
public static class AAH_ThingDefOf
{
    public static ThingDef AAH_ThanaticReactor;
    public static ThingDef AAH_ThanaticDrainParticle;
    public static ThingDef AAH_ThanaticAura;
    public static ThingDef AAH_ThanaticAuraLong;
    public static ThingDef AAH_ThanaticStreamController;
    public static ThingDef Mote_AAHReactorGlow;

    [MayRequire("Ludeon.RimWorld.Odyssey")]
    public static ThingDef AAH_GravReactor;
    [MayRequire("Ludeon.RimWorld.Odyssey")]
    public static ThingDef AAH_GravChargeAura;

    [MayRequire("VanillaExpanded.VFEPower")]
    public static ThingDef VPE_ArchotechViolenceGenerator;

    static AAH_ThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(AAH_ThingDefOf));
    }
}

[DefOf]
public static class AAH_DesignationDefOf
{
    // Shares its defName with the JobDef of the same name (see AAH_JobDefOf).
    public static DesignationDef AAH_ExtractThanaticReactor;

    static AAH_DesignationDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(AAH_DesignationDefOf));
    }
}

[DefOf]
public static class AAH_JobDefOf
{
    // Shares its defName with the DesignationDef of the same name (see AAH_DesignationDefOf).
    public static JobDef AAH_ExtractThanaticReactor;

    static AAH_JobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(AAH_JobDefOf));
    }
}

[DefOf]
public static class AAH_DefOf
{
    public static NeedDef VREA_ReactorPower;
    public static DamageDef AAH_ThanaticDepletion;
    public static RulePackDef AAH_Event_ThanaticDrain;
    public static InspirationDef AAH_SelfDetermination;
    public static GeneCategoryDef AAH_Hardware;
    public static ThoughtDef AAH_SelfDeterminationFulfilled;
    public static ThoughtDef AAH_SelfDeterminationOverridden;

    static AAH_DefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(AAH_DefOf));
    }
}
