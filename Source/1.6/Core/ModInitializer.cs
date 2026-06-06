using HarmonyLib;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Mod entry point. Holds settings; Harmony patching is done separately by
/// <see cref="ArchotechAndroidHardwareHarmony"/>.
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
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect);
    }

    public override string SettingsCategory() => "Archotech Android Hardware";
}

/// <summary>
/// Applies all Harmony patches, on the main thread.
///
/// Patching is done from a <see cref="StaticConstructorOnStartupAttribute"/>
/// class — guaranteed to run on the main thread after content has loaded —
/// rather than from the <see cref="Mod"/> constructor, which RimWorld runs on a
/// background loader thread. The thread matters: a couple of patches target
/// VREA's <c>Building_AndroidBehavioristStation</c>, which is itself
/// <c>[StaticConstructorOnStartup]</c> and <c>ContentFinder</c>-loads a texture
/// (<c>UI/Gizmos/EjectAnAndroid</c>) in its static constructor. Harmony-patching
/// that class triggers its static constructor; doing so off the main thread
/// raised "Tried to get a resource ... from a different thread". Patching here
/// keeps that initialization on the main thread.
/// </summary>
[StaticConstructorOnStartup]
internal static class ArchotechAndroidHardwareHarmony
{
    static ArchotechAndroidHardwareHarmony()
    {
        var harmony = new Harmony("shunter.archotechandroidhardware");
        harmony.PatchAll();
        Log.Message($"[Archotech Android Hardware] Initialized with {harmony.GetPatchedMethods().EnumerableCount()} patches.");
    }
}
