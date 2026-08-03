using Verse;

namespace ArchotechAndroidHardware;

// "Reactors" settings section — the reactor body-attachment visuals shared by
// every reactor (vanometric / thanatic / grav and VREA's stock reactor).
//
// Reactor core glow rendering — two layers:
//   • Body-attachment render node (PawnRenderNodeWorker_ReactorGlow) — ALWAYS
//     drawn. As a sibling of the chest attachment it inherits every body
//     transform automatically (posture, bed, carry, crawl, vanilla + third-party
//     animation) and also appears anywhere the pawn is portrait-rendered (the
//     colonist bar, the inspect-pane icon). Trade-off: it draws in the pawn's
//     altitude band, so unnatural darkness occludes it.
//   • Mote overlay (ReactorGlowMote → Mote_AAHReactorGlow) — OPTIONAL, gated by
//     the reactorGlowMoteOverlay setting. Drawn at AltitudeLayer.Darkness /
//     renderQueue 4000 so it punches through night and unnatural darkness (the
//     CompNoctolEyes trick). It is layered ON TOP of the render node, not in
//     place of it. Because that render order draws above almost everything it
//     can also cover other overlays (weapons, stun text, weather), and a mote
//     isn't in the render tree so ReactorGlowMote mirrors the body transform by
//     hand and hides in cases it can't track (crawling, carry, hidden body,
//     animations) — hence it's the opt-in extra rather than the baseline.
// The setting switches live: ReactorGlowMote.Maintain creates/keeps the mote
// only while reactorGlowMoteOverlay is on and tears it down when flipped off.
public partial class ArchotechAndroidHardwareSettings
{
    // Master per-source toggles for the reactor body-attachment visuals (chest
    // chassis sprite + always-on core-glow render node + the optional mote). When
    // off, the reactor renders no hardware on the body at all for that source — it
    // still functions, it just shows nothing. Split so players can hide this mod's
    // exotic reactors (vanometric / thanatic / grav) and/or the stock VREA reactor
    // independently; the VREA visuals are themselves added by this mod, so opting
    // out restores plain-VREA appearance. The two glow settings below are inert
    // when both are off. Gated in ReactorGlow.AttachmentsEnabledFor (render nodes
    // via CanDrawNow, the mote via ReactorGlowMote.Maintain).
    public bool renderAahReactorAttachments = DefaultRenderAahReactorAttachments;
    public bool renderVreaReactorAttachment = DefaultRenderVreaReactorAttachment;
    // Whenever a reactor's body attachment is drawn, the core-glow render node
    // draws with it; this toggles an additional darkness-piercing mote overlay
    // layered on top (see class doc).
    public bool reactorGlowMoteOverlay = DefaultReactorGlowMoteOverlay;
    public bool scaleReactorGlowByPower = DefaultScaleReactorGlowByPower;

    private const bool DefaultRenderAahReactorAttachments = true;
    private const bool DefaultRenderVreaReactorAttachment = true;
    private const bool DefaultReactorGlowMoteOverlay = true;
    private const bool DefaultScaleReactorGlowByPower = true;

    private void ExposeReactorSettings()
    {
        Scribe_Values.Look(ref renderAahReactorAttachments, "renderAahReactorAttachments", DefaultRenderAahReactorAttachments);
        Scribe_Values.Look(ref renderVreaReactorAttachment, "renderVreaReactorAttachment", DefaultRenderVreaReactorAttachment);
        Scribe_Values.Look(ref reactorGlowMoteOverlay, "reactorGlowMoteOverlay", DefaultReactorGlowMoteOverlay);
        Scribe_Values.Look(ref scaleReactorGlowByPower, "scaleReactorGlowByPower", DefaultScaleReactorGlowByPower);
    }

    private void ResetReactorSettings()
    {
        renderAahReactorAttachments = DefaultRenderAahReactorAttachments;
        renderVreaReactorAttachment = DefaultRenderVreaReactorAttachment;
        reactorGlowMoteOverlay = DefaultReactorGlowMoteOverlay;
        scaleReactorGlowByPower = DefaultScaleReactorGlowByPower;
    }

    private void DrawReactorsSection(Listing_Standard listing)
    {
        SectionHeader(listing, "AAH_SectionReactors".Translate());

        listing.CheckboxLabeled("AAH_RenderAahAttachments".Translate(),
            ref renderAahReactorAttachments,
            "AAH_RenderAahAttachmentsDesc".Translate());

        listing.Gap(6f);
        listing.CheckboxLabeled("AAH_RenderVreaAttachment".Translate(),
            ref renderVreaReactorAttachment,
            "AAH_RenderVreaAttachmentDesc".Translate());

        listing.Gap(12f);

        // The glow-mote overlay and opacity-scaling options only affect reactors that
        // are actually being drawn, so grey them out (and ignore clicks) when both
        // master toggles above are off — there's nothing for them to act on.
        bool anyReactorRendered = renderAahReactorAttachments || renderVreaReactorAttachment;

        CheckboxLabeled(listing, "AAH_ReactorGlowMote".Translate(),
            ref reactorGlowMoteOverlay,
            "AAH_ReactorGlowMoteDesc".Translate(),
            disabled: !anyReactorRendered);

        listing.Gap(6f);
        CheckboxLabeled(listing, "AAH_ScaleGlowByPower".Translate(),
            ref scaleReactorGlowByPower,
            "AAH_ScaleGlowByPowerDesc".Translate(),
            disabled: !anyReactorRendered);

        listing.Gap(30f);
    }
}
