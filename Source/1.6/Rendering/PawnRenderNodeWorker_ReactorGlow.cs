using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Render-node worker for the body-parented reactor core glow (the "safe" glow
/// mode). Behaves exactly like the chest attachment's
/// <see cref="PawnRenderNodeWorker_AttachmentBody"/> — inheriting body posture,
/// bed, carry, crawl and animation transforms plus body-size scaling — but only
/// draws when the player has selected <see cref="ReactorGlowMode.RenderNodeSafe"/>.
///
/// In <see cref="ReactorGlowMode.MoteExperimental"/> mode the glow is supplied by
/// <see cref="ReactorGlowMote"/> instead, so this node stays dormant. The check
/// is per-draw (CanDrawNow), so toggling the mod setting takes effect live with
/// no render-tree recache.
/// </summary>
public class PawnRenderNodeWorker_ReactorGlow : PawnRenderNodeWorker_AttachmentBody
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (ArchotechAndroidHardwareMod.Settings?.reactorGlowMode != ReactorGlowMode.RenderNodeSafe)
            return false;
        return base.CanDrawNow(node, parms);
    }

    /// <summary>
    /// Scales the glow's alpha by the android's power level each draw. The base
    /// implementation sets _Color to <c>parms.tint * material.color</c> on a
    /// reused <see cref="MaterialPropertyBlock"/>; we simply override the alpha
    /// with <see cref="ReactorGlow.GlowOpacity"/>. This rides the existing
    /// per-draw colour path RimWorld uses for tinting — no graphic rebake, no
    /// render-tree recache — so it tracks slow power drain for free. The node's
    /// XML <c>&lt;color&gt;</c> alpha is therefore irrelevant (its RGB still
    /// drives the tint); the code constant governs opacity.
    /// </summary>
    public override MaterialPropertyBlock GetMaterialPropertyBlock(PawnRenderNode node, Material material, PawnDrawParms parms)
    {
        var block = base.GetMaterialPropertyBlock(node, material, parms);
        if (block == null || parms.Statue) return block;

        Color c = parms.tint * material.color;   // mirror the base's non-statue colour
        c.a = ReactorGlow.GlowOpacity(parms.pawn) * parms.tint.a;
        block.SetColor(ShaderPropertyIDs.Color, c);
        return block;
    }
}
