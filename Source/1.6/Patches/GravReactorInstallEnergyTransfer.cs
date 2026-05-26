using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

/// <summary>
/// Transfers a Grav Reactor item's <c>storedEnergy</c> onto the installed
/// <see cref="Hediff_GravReactor"/> during install surgery. Without this,
/// every installation would start the hediff at Energy = 1f (PostAdd default),
/// wiping the ingredient item's stored charge and breaking reactor
/// transferability between android pawns.
///
/// Mirrors <see cref="ThanaticReactorInstallEnergyTransfer_Patch"/> exactly —
/// kept as separate types (rather than a shared generic patch) so each
/// reactor's energy-transfer pipeline is independently traceable in stack
/// dumps and Harmony's patch list. The runtime cost of the duplication is
/// trivial.
///
/// Coexists with <see cref="RecipeInstallAndroidPart_ApplyOnPawn_Patch"/> on
/// the same target method.
/// </summary>
[HarmonyPatch]
public static class GravReactorInstallEnergyTransfer_Patch
{
    private static float? _pendingEnergy;
    private static HediffDef _gravReactorDef;
    private static HediffDef GravReactorDef =>
        _gravReactorDef ??= DefDatabase<HediffDef>.GetNamed("AAH_GravReactor", errorOnFail: false);

    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("VREAndroids.Recipe_InstallAndroidPart");
        return type != null
            ? AccessTools.Method(type, "ApplyOnPawn",
                new[] { typeof(Pawn), typeof(BodyPartRecord), typeof(Pawn), typeof(List<Thing>), typeof(Bill) })
            : null;
    }

    [HarmonyPrefix]
    public static void Prefix(List<Thing> ingredients)
    {
        _pendingEnergy = null;
        if (ingredients == null) return;

        foreach (var thing in ingredients)
        {
            if (thing is GravReactorThing gr)
            {
                _pendingEnergy = gr.storedEnergy;
                return;
            }
        }
    }

    [HarmonyPostfix]
    public static void Postfix(Pawn pawn)
    {
        if (_pendingEnergy == null) return;
        var energy = _pendingEnergy.Value;
        _pendingEnergy = null;

        if (pawn?.health?.hediffSet == null) return;
        var def = GravReactorDef;
        if (def == null) return;

        if (pawn.health.hediffSet.GetFirstHediffOfDef(def) is Hediff_GravReactor hediff)
            hediff.Energy = energy;
    }
}
