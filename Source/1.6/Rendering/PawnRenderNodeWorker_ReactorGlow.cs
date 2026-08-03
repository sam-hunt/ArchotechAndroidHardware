using UnityEngine;
using Verse;

namespace ArchotechAndroidHardware;

// Render-node worker for the body-parented reactor core glow. Extends
// PawnRenderNodeWorker_ReactorAttachment (so it inherits the
// "Render … reactor body attachments" CanDrawNow gate — the glow appears and
// disappears together with the chest chassis), which in turn extends vanilla
// PawnRenderNodeWorker_AttachmentBody — so it tracks the torso
// everywhere the pawn is drawn, including the colonist bar and inspect-pane
// portrait, inheriting posture / bed / carry / crawl / animation transforms.
//
// While its master toggle is on, this node ALWAYS draws (subject only to the
// base worker's facing / body-visibility checks). The optional
// ReactorGlowMote overlay, gated by the reactorGlowMoteOverlay
// setting, layers on TOP of this node to punch through unnatural darkness — it
// does not replace it. The only customisation here is per-draw alpha (see
// GetMaterialPropertyBlock).
public class PawnRenderNodeWorker_ReactorGlow : PawnRenderNodeWorker_ReactorAttachment
{
    // Scales the glow's alpha by the android's power level each draw. The base
    // implementation sets _Color to parms.tint * material.color on a
    // reused MaterialPropertyBlock; we simply override the alpha
    // with ReactorGlow.GlowOpacity. This rides the existing
    // per-draw colour path RimWorld uses for tinting — no graphic rebake, no
    // render-tree recache — so it tracks slow power drain for free. The node's
    // XML <color> alpha is therefore irrelevant (its RGB still
    // drives the tint); the code constant governs opacity.
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
