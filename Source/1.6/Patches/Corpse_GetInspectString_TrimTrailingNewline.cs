using HarmonyLib;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// VREA workaround: strips the dangling trailing newline VREA leaves on android
// corpse inspect strings, which otherwise spams a red
// "Inspect string for ... contains empty lines" error every time the corpse's
// inspect pane draws.
//
// Problem: VREA's Corpse.GetInspectString postfix removes the vanilla
// "Body parts missing: X%" line from android corpses via a naive
// __result.Replace("Body parts missing: X%", ""). That line was added
// with AppendLine (text + '\n'), so the Replace deletes the text but
// leaves the preceding newline, ending the string in a trailing '\n'. Vanilla
// Corpse.GetInspectString trims trailing newlines at its tail, but
// VREA's postfix runs after that and re-introduces one.
// InspectPaneFiller.DrawInspectStringFor then flags the trailing newline
// via GenText.ContainsEmptyLines and logs Log.ErrorOnce.
//
// Our Thanatic reactor death (and the manual corpse extraction job) reliably
// triggers this: depletion ejects the reactor and adds a MissingBodyPart on
// the reactor slot, pushing the corpse's missing-parts coverage over the >=1%
// threshold that makes the "Body parts missing" line appear in the first place.
//
// Fix: re-apply TrimEndNewlines() after VREA's postfix runs — exactly
// what vanilla already does at its own tail. [HarmonyAfter] on VREA's
// Harmony id ("VREAndroidsMod") plus Priority.Last guarantees we run
// after VREA mutates the result. Idempotent no-op for any already-clean string.
//
// Removable if: VREA changes its replace to also drop the orphaned newline
// (e.g. removing the whole "...\n" line rather than just the text), or stops
// stripping the line altogether.
[HarmonyPatch(typeof(Corpse), nameof(Corpse.GetInspectString))]
[HarmonyAfter("VREAndroidsMod")]
public static class Corpse_GetInspectString_TrimTrailingNewline
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(ref string __result)
    {
        if (!string.IsNullOrEmpty(__result))
            __result = __result.TrimEndNewlines();
    }
}
