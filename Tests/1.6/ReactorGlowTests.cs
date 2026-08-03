using Xunit;

namespace ArchotechAndroidHardware.Tests
{
    // ReactorGlow (Source/1.6/Rendering/ReactorGlow.cs) is only partially
    // testable headless:
    //
    //   - AttachmentsEnabledFor(HediffDef) has two guard clauses that return
    //     early without touching anything but ArchotechAndroidHardwareMod.Settings
    //     (a plain static we can install), so those are covered below.
    //   - The branch beyond the guard clauses compares the def against
    //     AAH_HediffDefOf.VREA_Reactor. AAH_HediffDefOf is a [DefOf] class;
    //     touching any of its fields for the first time runs its static
    //     constructor, which calls RimWorld.DefOfHelper.EnsureInitializedInCtor.
    //     Outside a live game (DefOfHelper.RebindAllDefOfs never ran) that logs
    //     a Verse.Log.Warning, which calls into UnityEngine.Debug.LogWarning —
    //     an engine-native call whose behaviour outside a running Unity player
    //     isn't guaranteed. Not worth gambling a hard crash/hang for one more
    //     branch, so it's skipped rather than forced.
    //   - PowerFraction(Pawn) / GlowOpacity(Pawn) need a live Pawn with a
    //     working needs tracker (pawn.needs.TryGetNeed) — not constructible
    //     headless — so they're skipped entirely.
    public class ReactorGlowTests
    {
        [Fact]
        public void AttachmentsEnabledFor_NullSettings_ReturnsTrue()
        {
            var original = ArchotechAndroidHardwareMod.Settings;
            try
            {
                ArchotechAndroidHardwareMod.Settings = null;
                var reactorDef = TestHelpers.MakeHediffDef();

                Assert.True(ReactorGlow.AttachmentsEnabledFor(reactorDef));
            }
            finally
            {
                ArchotechAndroidHardwareMod.Settings = original;
            }
        }

        [Fact]
        public void AttachmentsEnabledFor_NullReactorDef_ReturnsTrue()
        {
            var original = ArchotechAndroidHardwareMod.Settings;
            try
            {
                TestHelpers.InstallDefaultSettings();

                Assert.True(ReactorGlow.AttachmentsEnabledFor(null));
            }
            finally
            {
                ArchotechAndroidHardwareMod.Settings = original;
            }
        }
    }
}
