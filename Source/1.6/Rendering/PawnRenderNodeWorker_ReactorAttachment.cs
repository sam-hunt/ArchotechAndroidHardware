using Verse;

namespace ArchotechAndroidHardware;

// Body-attachment render-node worker for the reactor chest chassis sprite,
// adding a single thing over the vanilla PawnRenderNodeWorker_AttachmentBody:
// a CanDrawNow gate on the "Render … reactor body attachments"
// settings (see ReactorGlow.AttachmentsEnabledFor). Because
// CanDrawNow is consulted per-draw (not at render-tree build), flipping
// the setting hides/shows the chassis live.
//
// PawnRenderNodeWorker_ReactorGlow extends this so the always-on
// core-glow node inherits the same gate — both halves of the reactor visual
// appear and disappear together. The chest chassis node uses this worker
// directly (wired in each reactor hediff's renderNodeProperties XML).
public class PawnRenderNodeWorker_ReactorAttachment : PawnRenderNodeWorker_AttachmentBody
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (!ReactorGlow.AttachmentsEnabledFor(node.hediff?.def)) return false;
        return base.CanDrawNow(node, parms);
    }
}
