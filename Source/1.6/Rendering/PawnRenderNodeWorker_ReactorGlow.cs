using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Render-node worker for the body-parented reactor core glow. Behaves exactly
/// like the chest attachment's <see cref="PawnRenderNodeWorker_AttachmentBody"/>
/// — inheriting body posture, bed, carry, crawl and animation transforms plus
/// body-size scaling — so it tracks the torso everywhere the pawn is drawn,
/// including the colonist bar and the inspect-pane portrait.
///
/// This node ALWAYS draws (subject only to the base worker's facing /
/// body-visibility checks). The optional <see cref="ReactorGlowMote"/> overlay,
/// gated by the <c>reactorGlowMoteOverlay</c> setting, layers on TOP of this node
/// to punch through unnatural darkness — it does not replace it. The only
/// customisation here is per-draw alpha (see <see cref="GetMaterialPropertyBlock"/>).
/// </summary>
public class PawnRenderNodeWorker_ReactorGlow : PawnRenderNodeWorker_AttachmentBody
{
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
