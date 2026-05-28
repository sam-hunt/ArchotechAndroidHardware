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
}
