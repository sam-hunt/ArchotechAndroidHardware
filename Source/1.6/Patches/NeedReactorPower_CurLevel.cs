using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// VREA workaround: wires Need_ReactorPower's UI bar AND dev-mode
// +/- controls to any installed AAH reactor hediff implementing
// IAAHReactorEnergy when VREA's own reactor hediff is absent.
//
// Problem: both accessors on VREA's Need_ReactorPower.CurLevel look
// up the backing hediff by def (VREA_DefOf.VREA_Reactor), not by
// type. When an AAH reactor (Thanatic / Grav) replaces VREA's reactor:
//   • Getter returns 0 — bar permanently reads empty,
//     Alert_AndroidsLowOnPower flags the pawn every tick.
//   • Setter silently no-ops — the dev-mode +/- buttons appear but do
//     nothing, making reactor behaviour hard to test in-game.
// The Need itself still exists because the VREA_Power gene's
// enablesNeeds creates it.
//
// Fix: Postfix both accessors. When VREA_Reactor is absent, enumerate the
// known AAH reactor defs (see ReactorDefs) and route
// through the first installed one that implements
// IAAHReactorEnergy. Getter reads Energy; setter writes Energy
// and syncs Need.curLevelInt (matches VREA's native setter behaviour
// when its reactor is installed).
//
// VREA_Reactor presence takes precedence in both directions — VREA's
// original accessors are left untouched in that branch.
//
// Adding a new reactor type: implement IAAHReactorEnergy on
// the new hediff and add its DefOf field to ReactorDefs.
//
// Removable if: VREA makes the reactor hediff lookup extensible (by tag,
// interface, or DefDatabase scan rather than a single hardcoded def).
internal static class NeedReactorPowerPatchHelpers
{
    private static HediffDef[] _reactorDefs;

    internal static HediffDef VreaReactorDef => AAH_HediffDefOf.VREA_Reactor;

    // Order matters: the first installed reactor wins. Defensive ordering —
    // a pawn shouldn't have more than one AAH reactor installed (they all
    // occupy the same Stomach slot), but if some external mod allows it,
    // Thanatic before Grav matches the historical default. AAH_GravReactor is
    // Odyssey-gated (null without Odyssey) and filtered out.
    internal static HediffDef[] ReactorDefs => _reactorDefs ??= new[]
    {
        AAH_HediffDefOf.AAH_ThanaticReactor,
        AAH_HediffDefOf.AAH_GravReactor,
    }.Where(d => d != null).ToArray();

    internal static IAAHReactorEnergy FindReactor(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null) return null;
        var defs = ReactorDefs;
        for (int i = 0; i < defs.Length; i++)
        {
            if (pawn.health.hediffSet.GetFirstHediffOfDef(defs[i]) is IAAHReactorEnergy energy)
                return energy;
        }
        return null;
    }

    internal static System.Type NeedReactorPowerType() =>
        AccessTools.TypeByName("VREAndroids.Need_ReactorPower");
}

[HarmonyPatch]
public static class NeedReactorPower_CurLevelGetter_Patch
{
    static MethodBase TargetMethod()
    {
        var type = NeedReactorPowerPatchHelpers.NeedReactorPowerType();
        return type != null ? AccessTools.PropertyGetter(type, "CurLevel") : null;
    }

    [HarmonyPostfix]
    public static void Postfix(ref float __result, Pawn ___pawn)
    {
        var pawn = ___pawn;
        if (pawn?.health?.hediffSet == null) return;

        var vreaDef = NeedReactorPowerPatchHelpers.VreaReactorDef;
        if (vreaDef != null && pawn.health.hediffSet.HasHediff(vreaDef))
            return;

        var reactor = NeedReactorPowerPatchHelpers.FindReactor(pawn);
        if (reactor != null)
            __result = reactor.Energy;
    }
}

[HarmonyPatch]
public static class NeedReactorPower_CurLevelSetter_Patch
{
    // Need.curLevelInt is protected; Harmony FieldRefAccess bypasses access modifiers.
    private static readonly AccessTools.FieldRef<Need, float> _curLevelIntRef =
        AccessTools.FieldRefAccess<Need, float>("curLevelInt");

    static MethodBase TargetMethod()
    {
        var type = NeedReactorPowerPatchHelpers.NeedReactorPowerType();
        return type != null ? AccessTools.PropertySetter(type, "CurLevel") : null;
    }

    [HarmonyPostfix]
    public static void Postfix(float value, Need __instance, Pawn ___pawn)
    {
        var pawn = ___pawn;
        if (pawn?.health?.hediffSet == null) return;

        // VREA's reactor present → its original setter already wrote the value. Skip.
        var vreaDef = NeedReactorPowerPatchHelpers.VreaReactorDef;
        if (vreaDef != null && pawn.health.hediffSet.HasHediff(vreaDef))
            return;

        var reactor = NeedReactorPowerPatchHelpers.FindReactor(pawn);
        if (reactor == null) return;

        reactor.Energy = value;                       // setter clamps to [0,1]
        _curLevelIntRef(__instance) = reactor.Energy; // mirror VREA's native curLevelInt sync
    }
}
