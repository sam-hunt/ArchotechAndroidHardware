using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// VREA workaround: wires <c>Need_ReactorPower</c>'s UI bar AND dev-mode
/// +/- controls to our Thanatic Reactor hediff when VREA's own reactor
/// hediff is absent.
///
/// Problem: both accessors on VREA's <c>Need_ReactorPower.CurLevel</c> look
/// up the backing hediff by def (<c>VREA_DefOf.VREA_Reactor</c>), not by
/// type. When a Thanatic Reactor replaces VREA's reactor:
///   • Getter returns 0 — bar permanently reads empty,
///     <c>Alert_AndroidsLowOnPower</c> flags the pawn every tick.
///   • Setter silently no-ops — the dev-mode +/- buttons
///     (<c>Need.OffsetDebugPercent</c> → <c>CurLevel</c> +=) appear but do
///     nothing, making reactor behaviour hard to test in-game.
/// The Need itself still exists because the <c>VREA_Power</c> gene's
/// <c>enablesNeeds</c> creates it.
///
/// Fix: Postfix both accessors. When VREA_Reactor is absent and our
/// Thanatic Reactor is present, the getter falls through to the hediff's
/// Energy and the setter writes back into it (plus syncs the base
/// <c>Need.curLevelInt</c> for save consistency — mirrors VREA's own setter
/// behaviour when its reactor is installed).
///
/// VREA_Reactor presence takes precedence in both directions: the getter
/// leaves the result alone and the setter lets VREA's original write stand.
/// The patch is a strict superset of VREA's behaviour.
///
/// Removable if: VREA makes the reactor hediff lookup extensible (by tag,
/// interface, or DefDatabase scan rather than a single hardcoded def).
/// </summary>
internal static class NeedReactorPowerPatchHelpers
{
    private static HediffDef _vreaReactorDef;
    private static bool _vreaReactorDefResolved;
    private static HediffDef _thanaticReactorDef;

    internal static HediffDef VreaReactorDef
    {
        get
        {
            if (_vreaReactorDefResolved) return _vreaReactorDef;
            _vreaReactorDefResolved = true;
            _vreaReactorDef = DefDatabase<HediffDef>.GetNamed("VREA_Reactor", errorOnFail: false);
            return _vreaReactorDef;
        }
    }

    internal static HediffDef ThanaticReactorDef =>
        _thanaticReactorDef ??= DefDatabase<HediffDef>.GetNamed("AAH_ThanaticReactor", errorOnFail: false);

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

        var thanaticDef = NeedReactorPowerPatchHelpers.ThanaticReactorDef;
        if (thanaticDef == null) return;
        if (pawn.health.hediffSet.GetFirstHediffOfDef(thanaticDef) is Hediff_ThanaticReactor thanatic)
            __result = thanatic.Energy;
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

        var thanaticDef = NeedReactorPowerPatchHelpers.ThanaticReactorDef;
        if (thanaticDef == null) return;
        if (pawn.health.hediffSet.GetFirstHediffOfDef(thanaticDef) is not Hediff_ThanaticReactor thanatic)
            return;

        thanatic.Energy = value;                        // Energy setter clamps to [0,1]
        _curLevelIntRef(__instance) = thanatic.Energy;  // mirror VREA's native curLevelInt sync
    }
}
