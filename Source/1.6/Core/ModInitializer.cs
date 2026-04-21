using HarmonyLib;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Entry point. Applies all Harmony patches at startup.
///
/// Most patches in this mod are VREA workarounds that compensate for VREA's
/// assumptions about android reactor types and hediff management. Each patch
/// documents which VREA behavior it compensates for and when it can be removed.
/// See the individual patch classes in the VREAPatches namespace for details.
/// </summary>
[StaticConstructorOnStartup]
public static class ArchotechAndroidHardwareMod
{
    static ArchotechAndroidHardwareMod()
    {
        var harmony = new Harmony("shunter.archotechandroidhardware");
        harmony.PatchAll();
        Log.Message($"[Archotech Android Hardware] Initialized with {harmony.GetPatchedMethods().EnumerableCount()} patches.");
    }
}
