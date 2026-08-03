using HarmonyLib;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Mod entry point. Holds settings; Harmony patching is done separately by
// ArchotechAndroidHardwareHarmony.
//
// Most patches in this mod are VREA workarounds that compensate for VREA's
// assumptions about android reactor types and hediff management. Each patch
// documents which VREA behavior it compensates for and when it can be removed.
// See the individual patch classes in the VREAPatches namespace for details.
public class ArchotechAndroidHardwareMod : Mod
{
    // Setter is internal so the headless test suite can install a settings instance.
    public static ArchotechAndroidHardwareSettings Settings { get; internal set; }

    public ArchotechAndroidHardwareMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ArchotechAndroidHardwareSettings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect);
    }

    // Re-apply def-mutating settings when the window closes (the psychic-sensitivity
    // offset is pushed into the hediff stage here, not read live like the reactor knobs).
    public override void WriteSettings()
    {
        base.WriteSettings();
        Settings.ApplyTransceiverSensitivityOffset();
    }

    public override string SettingsCategory() => "AAH_SettingsCategory".Translate();
}

// Applies all Harmony patches, on the main thread.
//
// Patching is done from a [StaticConstructorOnStartup] class — guaranteed to
// run on the main thread after content has loaded — rather than from the Mod
// constructor, which RimWorld runs on a background loader thread. The thread
// matters: a couple of patches target VREA's Building_AndroidBehavioristStation,
// which is itself [StaticConstructorOnStartup] and ContentFinder-loads a
// texture (UI/Gizmos/EjectAnAndroid) in its static constructor. Harmony-patching
// that class triggers its static constructor; doing so off the main thread
// raised "Tried to get a resource ... from a different thread". Patching here
// keeps that initialization on the main thread.
[StaticConstructorOnStartup]
internal static class ArchotechAndroidHardwareHarmony
{
    static ArchotechAndroidHardwareHarmony()
    {
        var harmony = new Harmony("shunter.archotechandroidhardware");
        harmony.PatchAll();
        Log.Message($"[Archotech Android Hardware] Initialized with {harmony.GetPatchedMethods().EnumerableCount()} patches.");

        // Defs are loaded by now; push the saved psychic-sensitivity offset into the
        // transceiver hediff so it reflects the setting from the first stat query.
        ArchotechAndroidHardwareMod.Settings?.ApplyTransceiverSensitivityOffset();
    }
}
