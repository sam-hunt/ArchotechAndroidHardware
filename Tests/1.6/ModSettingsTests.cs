using Xunit;

namespace ArchotechAndroidHardware.Tests
{
    // Unit coverage for ArchotechAndroidHardwareSettings: ResetToDefaults()
    // restoring the exact same values a fresh instance's field initializers
    // produce, and a couple of sentinel-vs-default sanity checks.
    //
    // Unlike the sibling mods, this settings class's Default* constants are
    // all `private` (one per Core/Settings/Settings_*.cs partial file) rather
    // than public, so tests here can't assert against them by name. Instead,
    // ResetToDefaults_MatchesFreshInstanceDefaults compares a mutated-then-reset
    // instance against an untouched fresh instance field-by-field — this
    // structurally guarantees the two stay in sync (field initializer vs.
    // ResetToDefaults()) without duplicating any magic numbers, and fails
    // loudly if either one is edited without the other.
    public class ModSettingsTests
    {
        [Fact]
        public void ResetToDefaults_MatchesFreshInstanceDefaults()
        {
            var fresh = new ArchotechAndroidHardwareSettings();

            var mutated = new ArchotechAndroidHardwareSettings
            {
                renderAahReactorAttachments = !fresh.renderAahReactorAttachments,
                renderVreaReactorAttachment = !fresh.renderVreaReactorAttachment,
                reactorGlowMoteOverlay = !fresh.reactorGlowMoteOverlay,
                scaleReactorGlowByPower = !fresh.scaleReactorGlowByPower,

                thanaticRefillAmount = fresh.thanaticRefillAmount + 0.1f,
                thanaticOverchargeHoursPerUnit = fresh.thanaticOverchargeHoursPerUnit + 5f,
                thanaticOverchargeCapHours = fresh.thanaticOverchargeCapHours + 10f,
                overrideViolenceGeneratorSalvage = !fresh.overrideViolenceGeneratorSalvage,

                gravRefillAmount = fresh.gravRefillAmount + 0.1f,
                gravOverchargeHoursPerUnit = fresh.gravOverchargeHoursPerUnit + 5f,
                gravOverchargeCapHours = fresh.gravOverchargeCapHours + 10f,

                transceiverSensitivityOffset = fresh.transceiverSensitivityOffset + 0.5f,
                enableTransceiverReprogramming = !fresh.enableTransceiverReprogramming,

                enableSelfDeterminationInspiration = !fresh.enableSelfDeterminationInspiration,
                selfDeterminationCommonalityWithPart = fresh.selfDeterminationCommonalityWithPart + 1f,
                selfDeterminationCommonalityWithoutPart = fresh.selfDeterminationCommonalityWithoutPart + 1f,
            };

            // Sanity: every field above actually changed, so the reset assertions
            // below can't pass vacuously because a mutation was a no-op.
            Assert.NotEqual(fresh.renderAahReactorAttachments, mutated.renderAahReactorAttachments);
            Assert.NotEqual(fresh.renderVreaReactorAttachment, mutated.renderVreaReactorAttachment);
            Assert.NotEqual(fresh.reactorGlowMoteOverlay, mutated.reactorGlowMoteOverlay);
            Assert.NotEqual(fresh.scaleReactorGlowByPower, mutated.scaleReactorGlowByPower);
            Assert.NotEqual(fresh.thanaticRefillAmount, mutated.thanaticRefillAmount);
            Assert.NotEqual(fresh.thanaticOverchargeHoursPerUnit, mutated.thanaticOverchargeHoursPerUnit);
            Assert.NotEqual(fresh.thanaticOverchargeCapHours, mutated.thanaticOverchargeCapHours);
            Assert.NotEqual(fresh.overrideViolenceGeneratorSalvage, mutated.overrideViolenceGeneratorSalvage);
            Assert.NotEqual(fresh.gravRefillAmount, mutated.gravRefillAmount);
            Assert.NotEqual(fresh.gravOverchargeHoursPerUnit, mutated.gravOverchargeHoursPerUnit);
            Assert.NotEqual(fresh.gravOverchargeCapHours, mutated.gravOverchargeCapHours);
            Assert.NotEqual(fresh.transceiverSensitivityOffset, mutated.transceiverSensitivityOffset);
            Assert.NotEqual(fresh.enableTransceiverReprogramming, mutated.enableTransceiverReprogramming);
            Assert.NotEqual(fresh.enableSelfDeterminationInspiration, mutated.enableSelfDeterminationInspiration);
            Assert.NotEqual(fresh.selfDeterminationCommonalityWithPart, mutated.selfDeterminationCommonalityWithPart);
            Assert.NotEqual(fresh.selfDeterminationCommonalityWithoutPart, mutated.selfDeterminationCommonalityWithoutPart);

            mutated.ResetToDefaults();

            Assert.Equal(fresh.renderAahReactorAttachments, mutated.renderAahReactorAttachments);
            Assert.Equal(fresh.renderVreaReactorAttachment, mutated.renderVreaReactorAttachment);
            Assert.Equal(fresh.reactorGlowMoteOverlay, mutated.reactorGlowMoteOverlay);
            Assert.Equal(fresh.scaleReactorGlowByPower, mutated.scaleReactorGlowByPower);
            Assert.Equal(fresh.thanaticRefillAmount, mutated.thanaticRefillAmount);
            Assert.Equal(fresh.thanaticOverchargeHoursPerUnit, mutated.thanaticOverchargeHoursPerUnit);
            Assert.Equal(fresh.thanaticOverchargeCapHours, mutated.thanaticOverchargeCapHours);
            Assert.Equal(fresh.overrideViolenceGeneratorSalvage, mutated.overrideViolenceGeneratorSalvage);
            Assert.Equal(fresh.gravRefillAmount, mutated.gravRefillAmount);
            Assert.Equal(fresh.gravOverchargeHoursPerUnit, mutated.gravOverchargeHoursPerUnit);
            Assert.Equal(fresh.gravOverchargeCapHours, mutated.gravOverchargeCapHours);
            Assert.Equal(fresh.transceiverSensitivityOffset, mutated.transceiverSensitivityOffset);
            Assert.Equal(fresh.enableTransceiverReprogramming, mutated.enableTransceiverReprogramming);
            Assert.Equal(fresh.enableSelfDeterminationInspiration, mutated.enableSelfDeterminationInspiration);
            Assert.Equal(fresh.selfDeterminationCommonalityWithPart, mutated.selfDeterminationCommonalityWithPart);
            Assert.Equal(fresh.selfDeterminationCommonalityWithoutPart, mutated.selfDeterminationCommonalityWithoutPart);
        }

        [Fact]
        public void TwoFreshInstances_HaveIdenticalFieldValues()
        {
            // Guards against any field initializer depending on non-deterministic
            // state (e.g. accidentally reading Rand); two independently
            // constructed instances must agree on every default.
            var a = new ArchotechAndroidHardwareSettings();
            var b = new ArchotechAndroidHardwareSettings();

            Assert.Equal(a.renderAahReactorAttachments, b.renderAahReactorAttachments);
            Assert.Equal(a.renderVreaReactorAttachment, b.renderVreaReactorAttachment);
            Assert.Equal(a.reactorGlowMoteOverlay, b.reactorGlowMoteOverlay);
            Assert.Equal(a.scaleReactorGlowByPower, b.scaleReactorGlowByPower);
            Assert.Equal(a.thanaticRefillAmount, b.thanaticRefillAmount);
            Assert.Equal(a.thanaticOverchargeHoursPerUnit, b.thanaticOverchargeHoursPerUnit);
            Assert.Equal(a.thanaticOverchargeCapHours, b.thanaticOverchargeCapHours);
            Assert.Equal(a.overrideViolenceGeneratorSalvage, b.overrideViolenceGeneratorSalvage);
            Assert.Equal(a.gravRefillAmount, b.gravRefillAmount);
            Assert.Equal(a.gravOverchargeHoursPerUnit, b.gravOverchargeHoursPerUnit);
            Assert.Equal(a.gravOverchargeCapHours, b.gravOverchargeCapHours);
            Assert.Equal(a.transceiverSensitivityOffset, b.transceiverSensitivityOffset);
            Assert.Equal(a.enableTransceiverReprogramming, b.enableTransceiverReprogramming);
            Assert.Equal(a.enableSelfDeterminationInspiration, b.enableSelfDeterminationInspiration);
            Assert.Equal(a.selfDeterminationCommonalityWithPart, b.selfDeterminationCommonalityWithPart);
            Assert.Equal(a.selfDeterminationCommonalityWithoutPart, b.selfDeterminationCommonalityWithoutPart);
        }

        [Fact]
        public void ThanaticOverchargeCapHours_DefaultIsBelowTheUnlimitedSentinel()
        {
            // Guards against a future default edit accidentally landing on (or past)
            // the "no cap" sentinel the slider's top notch represents.
            var settings = new ArchotechAndroidHardwareSettings();

            Assert.True(settings.thanaticOverchargeCapHours < ArchotechAndroidHardwareSettings.ThanaticOverchargeCapUnlimited);
        }

        [Fact]
        public void GravOverchargeCapHours_DefaultIsBelowTheUnlimitedSentinel()
        {
            var settings = new ArchotechAndroidHardwareSettings();

            Assert.True(settings.gravOverchargeCapHours < ArchotechAndroidHardwareSettings.GravOverchargeCapUnlimited);
        }
    }
}
