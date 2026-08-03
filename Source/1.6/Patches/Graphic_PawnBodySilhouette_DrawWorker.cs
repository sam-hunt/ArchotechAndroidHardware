using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware.VREAPatches;

// Vanilla workaround: per-instance scroll-speed overrides for silhouette
// aura motes, plus a fix for a silent case-sensitivity bug in Core's
// _DetailScrollSpeed binding.
//
// Problem 1 (per-instance override): MaterialPool.MatFrom bakes scroll
// speeds into a pooled Material when the material is first created,
// from the XML shaderParameters list. After creation, no code path
// re-applies them — every mote sharing that graphicData renders with
// the same scroll vectors. For the directional victim aura we need
// per-kill scroll vectors (speckles pointing toward the killer) that
// can't be expressed as a single XML-baked constant.
//
// Problem 2 (Core case bug): Material.SetVector's property name is
// hashed case-sensitively. The MoteMultiplyAddScroll shader declares
// the detail-layer uniform as Pascal-case _DetailScrollSpeed, but
// every Core XML (e.g. Mote_ResurrectAbility) writes lowercase
// _detailScrollSpeed — a silent no-op. The detail layer therefore
// runs at the shader's compiled-in default (0.5, 0.5, 0, 1) across
// every vanilla silhouette mote, and any mod that copies the same
// XML lowercase form (as this mod originally did) is also ignored.
//
// Fix: Postfix runs after vanilla DrawWorker has populated the private
// bodyMaterial / headMaterial fields. If the mote is one of ours with
// overrideScroll=true, we read those materials via reflection and
// write the three scroll-speed params using the correct case
// (lowercase for texA/texB, Pascal for detail). Scroll vectors come
// in as world-space directions — the shader samples in world space
// via _pawnCenterWorld, so there's no mesh-rotation math to do.
//
// Shared-pool caveat: MaterialPool.MatFrom returns a shared Material
// for identical requests, so multiple silhouette motes in the same
// frame can clobber each other's shader params. Vanilla already does
// this for PawnCenterWorld / AgeSecs / Color, so it's an accepted
// baseline quirk.
//
// Removable if: RimWorld exposes scroll vectors on Graphic_Mote the
// way it already exposes PawnCenterWorld, and Ludeon fixes the
// lowercase detail-speed typo in Core XML (or the shader is changed
// to declare lowercase). Both would be welcome upstream changes.
[HarmonyPatch(typeof(Graphic_PawnBodySilhouette), nameof(Graphic_PawnBodySilhouette.DrawWorker))]
public static class Graphic_PawnBodySilhouette_DrawWorker_Patch
{
    private static readonly FieldInfo BodyMaterialField =
        AccessTools.Field(typeof(Graphic_PawnBodySilhouette), "bodyMaterial");
    private static readonly FieldInfo HeadMaterialField =
        AccessTools.Field(typeof(Graphic_PawnBodySilhouette), "headMaterial");

    // Property IDs cached once to avoid per-draw string hashing. Note the
    // mixed casing: texA/texB are lowercase in the shader, detail is
    // Pascal — see class summary for the Core case-bug details.
    private static readonly int TexAScrollSpeedId = Shader.PropertyToID("_texAScrollSpeed");
    private static readonly int TexBScrollSpeedId = Shader.PropertyToID("_texBScrollSpeed");
    private static readonly int DetailScrollSpeedId = Shader.PropertyToID("_DetailScrollSpeed");

    static void Postfix(Graphic_PawnBodySilhouette __instance, Thing thing)
    {
        if (thing is not Mote_ThanaticSilhouetteAura aura || !aura.overrideScroll)
            return;
        if (BodyMaterialField == null || HeadMaterialField == null)
            return;

        var bodyMat = BodyMaterialField.GetValue(__instance) as Material;
        var headMat = HeadMaterialField.GetValue(__instance) as Material;

        ApplyScroll(bodyMat, aura);
        ApplyScroll(headMat, aura);
    }

    private static void ApplyScroll(Material mat, Mote_ThanaticSilhouetteAura aura)
    {
        if (mat == null) return;
        // Shader reads scroll speed as a Vector (observed usages pass Vector3
        // in XML, which Unity promotes to Vector4). Components beyond .xy
        // are unused by this shader; zero them so we don't carry stale data
        // from the pool.
        mat.SetVector(TexAScrollSpeedId, new Vector4(aura.texAScroll.x, aura.texAScroll.y, 0f, 0f));
        mat.SetVector(TexBScrollSpeedId, new Vector4(aura.texBScroll.x, aura.texBScroll.y, 0f, 0f));
        mat.SetVector(DetailScrollSpeedId, new Vector4(aura.detailScroll.x, aura.detailScroll.y, 0f, 0f));
    }
}
