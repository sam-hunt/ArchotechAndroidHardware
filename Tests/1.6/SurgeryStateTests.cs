using Xunit;

namespace ArchotechAndroidHardware.Tests
{
    // SurgeryState.SuppressBodyPartDestruction is a bare internal static bool
    // with no logic of its own — this is a thin smoke test confirming (a) the
    // test assembly can actually reach it via InternalsVisibleTo and (b) it
    // behaves as a plain mutable flag, not something with a hidden setter side
    // effect. It is a process-wide static, so the test restores the prior
    // value in a finally to avoid leaking state to any other test.
    public class SurgeryStateTests
    {
        [Fact]
        public void SuppressBodyPartDestruction_RoundTripsAsAPlainMutableFlag()
        {
            bool original = SurgeryState.SuppressBodyPartDestruction;
            try
            {
                SurgeryState.SuppressBodyPartDestruction = true;
                Assert.True(SurgeryState.SuppressBodyPartDestruction);

                SurgeryState.SuppressBodyPartDestruction = false;
                Assert.False(SurgeryState.SuppressBodyPartDestruction);
            }
            finally
            {
                SurgeryState.SuppressBodyPartDestruction = original;
            }
        }
    }
}
