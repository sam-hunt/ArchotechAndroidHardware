using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Optional, setting-gated override (Vanilla Power Expanded only, default off):
/// rewrites the VPE Archotech Violence Generator's costList so that deconstructing
/// one by normal vanilla means yields 250 steel + 3 thanatic reactors.
///
/// Vanilla deconstruct refunds 50% of costList (Verse.GenLeaving — the fraction is
/// ThingDef.resourcesFractionWhenDeconstructed, default 0.5, RoundRandom and capped
/// at count), so a costList of 500 steel + 6 reactors halves cleanly to 250 steel +
/// 3 reactors. The `intricate` flag does NOT reduce deconstruct refunds, so the
/// reactors come back in full. The generator's MarketValue is an explicit statBase
/// (3,400), which short-circuits the cost-derived value, so this costList change does
/// NOT alter its trade value. The generator has no vanilla build recipe, so its build
/// cost is moot — only the deconstruct refund is observable.
///
/// Runs at StaticConstructorOnStartup, which fires after both defs and mod settings
/// have loaded, so Settings is populated and the generator def (if present) resolves.
/// DefOf fields (ThingDefOf.Steel) are bound before static constructors run. Toggling
/// the setting requires a game restart to take or revert the change.
/// </summary>
[StaticConstructorOnStartup]
public static class ViolenceGeneratorSalvageOverride
{
    static ViolenceGeneratorSalvageOverride()
    {
        if (ArchotechAndroidHardwareMod.Settings == null
            || !ArchotechAndroidHardwareMod.Settings.overrideViolenceGeneratorSalvage)
            return;

        ThingDef generator = AAH_ThingDefOf.VPE_ArchotechViolenceGenerator;
        if (generator == null)
            return; // Vanilla Power Expanded not loaded — nothing to override.

        ThingDef reactor = AAH_ThingDefOf.AAH_ThanaticReactor;
        if (reactor == null)
        {
            Log.Warning("[Archotech Android Hardware] AAH_ThanaticReactor def missing; skipping violence generator salvage override.");
            return;
        }

        generator.costList = new List<ThingDefCountClass>
        {
            new ThingDefCountClass(ThingDefOf.Steel, 500),
            new ThingDefCountClass(reactor, 6),
        };
        Log.Message("[Archotech Android Hardware] Violence generator salvage override active: vanilla deconstruct now yields 250 steel + 3 thanatic reactors.");
    }
}
