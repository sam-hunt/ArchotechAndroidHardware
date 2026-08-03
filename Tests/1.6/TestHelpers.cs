using System.Runtime.Serialization;
using Verse;

namespace ArchotechAndroidHardware.Tests
{
    // Synthetic data builders + shared setup for the headless suite.
    internal static class TestHelpers
    {
        // Calling `new HediffDef()` runs Def's instance ctor, which assigns
        // debugRandomId via Verse.Rand.RangeInclusive — safe on its own, but
        // there's no reason to depend on Rand's static state being valid
        // outside a live game. GetUninitializedObject allocates without
        // running any constructor, matching the pattern the sibling mods'
        // TestHelpers use for their own Def subtypes.
        public static HediffDef MakeHediffDef(string defName = null)
        {
            var def = (HediffDef)FormatterServices.GetUninitializedObject(typeof(HediffDef));
            def.defName = defName ?? "TestHediffDef";
            return def;
        }

        // Installs a fresh, default-valued settings instance as the mod-wide
        // static (the setter is internal specifically so this test assembly
        // can reach it via InternalsVisibleTo — see ArchotechAndroidHardwareMod).
        public static ArchotechAndroidHardwareSettings InstallDefaultSettings()
        {
            var settings = new ArchotechAndroidHardwareSettings();
            ArchotechAndroidHardwareMod.Settings = settings;
            return settings;
        }
    }
}
