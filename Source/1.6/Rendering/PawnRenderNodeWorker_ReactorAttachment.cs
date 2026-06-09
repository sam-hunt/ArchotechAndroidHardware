using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// Body-attachment render-node worker for the reactor chest chassis sprite,
/// adding a single thing over the vanilla <see cref="PawnRenderNodeWorker_AttachmentBody"/>:
/// a <see cref="CanDrawNow"/> gate on the "Render … reactor body attachments"
/// settings (see <see cref="ReactorGlow.AttachmentsEnabledFor"/>). Because
/// <c>CanDrawNow</c> is consulted per-draw (not at render-tree build), flipping
/// the setting hides/shows the chassis live.
///
/// <see cref="PawnRenderNodeWorker_ReactorGlow"/> extends this so the always-on
/// core-glow node inherits the same gate — both halves of the reactor visual
/// appear and disappear together. The chest chassis node uses this worker
/// directly (wired in each reactor hediff's renderNodeProperties XML).
/// </summary>
public class PawnRenderNodeWorker_ReactorAttachment : PawnRenderNodeWorker_AttachmentBody
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (!ReactorGlow.AttachmentsEnabledFor(node.hediff?.def)) return false;
        return base.CanDrawNow(node, parms);
    }
}
