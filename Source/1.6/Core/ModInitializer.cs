using HarmonyLib;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Mod entry point. Wires up settings and applies all Harmony patches at startup.
///
/// Most patches in this mod are VREA workarounds that compensate for VREA's
/// assumptions about android reactor types and hediff management. Each patch
/// documents which VREA behavior it compensates for and when it can be removed.
/// See the individual patch classes in the VREAPatches namespace for details.
/// </summary>
public class ArchotechAndroidHardwareMod : Mod
{
    public static ArchotechAndroidHardwareSettings Settings { get; private set; }

    public ArchotechAndroidHardwareMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ArchotechAndroidHardwareSettings>();
        var harmony = new Harmony("shunter.archotechandroidhardware");
        harmony.PatchAll();
        Log.Message($"[Archotech Android Hardware] Initialized with {harmony.GetPatchedMethods().EnumerableCount()} patches.");
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect);
    }

    public override string SettingsCategory() => "Archotech Android Hardware";
}
