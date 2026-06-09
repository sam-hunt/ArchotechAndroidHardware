using Verse;

namespace ArchotechAndroidHardware;

/// <summary>
/// "Reactors" settings section — the reactor body-attachment visuals shared by
/// every reactor (vanometric / thanatic / grav and VREA's stock reactor).
///
/// Reactor core glow rendering — two layers:
///   • Body-attachment render node (PawnRenderNodeWorker_ReactorGlow) — ALWAYS
///     drawn. As a sibling of the chest attachment it inherits every body
///     transform automatically (posture, bed, carry, crawl, vanilla + third-party
///     animation) and also appears anywhere the pawn is portrait-rendered (the
///     colonist bar, the inspect-pane icon). Trade-off: it draws in the pawn's
///     altitude band, so unnatural darkness occludes it.
///   • Mote overlay (ReactorGlowMote → Mote_AAHReactorGlow) — OPTIONAL, gated by
///     the reactorGlowMoteOverlay setting. Drawn at AltitudeLayer.Darkness /
///     renderQueue 4000 so it punches through night and unnatural darkness (the
///     CompNoctolEyes trick). It is layered ON TOP of the render node, not in
///     place of it. Because that render order draws above almost everything it
///     can also cover other overlays (weapons, stun text, weather), and a mote
///     isn't in the render tree so ReactorGlowMote mirrors the body transform by
///     hand and hides in cases it can't track (crawling, carry, hidden body,
///     animations) — hence it's the opt-in extra rather than the baseline.
/// The setting switches live: ReactorGlowMote.Maintain creates/keeps the mote
/// only while reactorGlowMoteOverlay is on and tears it down when flipped off.
/// </summary>
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
    public bool renderAahReactorAttachments = true;
    public bool renderVreaReactorAttachment = true;
    // Whenever a reactor's body attachment is drawn, the core-glow render node
    // draws with it; this toggles an additional darkness-piercing mote overlay
    // layered on top (see class doc).
    public bool reactorGlowMoteOverlay = true;
    public bool scaleReactorGlowByPower = true;

    private void ExposeReactorSettings()
    {
        Scribe_Values.Look(ref renderAahReactorAttachments, "renderAahReactorAttachments", true);
        Scribe_Values.Look(ref renderVreaReactorAttachment, "renderVreaReactorAttachment", true);
        Scribe_Values.Look(ref reactorGlowMoteOverlay, "reactorGlowMoteOverlay", true);
        Scribe_Values.Look(ref scaleReactorGlowByPower, "scaleReactorGlowByPower", true);
    }

    private void ResetReactorSettings()
    {
        renderAahReactorAttachments = true;
        renderVreaReactorAttachment = true;
        reactorGlowMoteOverlay = true;
        scaleReactorGlowByPower = true;
    }

    private void DrawReactorsSection(Listing_Standard listing)
    {
        SectionHeader(listing, "Reactors");

        listing.CheckboxLabeled("Render archotech reactor body attachments",
            ref renderAahReactorAttachments,
            "When enabled, androids display the chest housing and core glow for installed archotech " +
            "reactors (the vanometric, thanatic, and grav reactors added by this mod). When disabled, " +
            "those reactors render no hardware on the body — they still function, they just stay hidden.");

        listing.Gap(6f);
        listing.CheckboxLabeled("Render VREA reactor body attachment",
            ref renderVreaReactorAttachment,
            "When enabled, androids display a chest housing and core glow for VREA's standard reactor. " +
            "This visual is added by Archotech Android Hardware so a stock android matches one with " +
            "archotech hardware installed; disable it to restore the plain VREA appearance.");

        listing.Gap(12f);

        // The glow-mote overlay and opacity-scaling options only affect reactors that
        // are actually being drawn, so grey them out (and ignore clicks) when both
        // master toggles above are off — there's nothing for them to act on.
        bool anyReactorRendered = renderAahReactorAttachments || renderVreaReactorAttachment;

        CheckboxLabeled(listing, "Render reactor glow mote (experimental)",
            ref reactorGlowMoteOverlay,
            "When enabled, an extra glow layer is rendered for the reactor core at a very high render order, " +
            "piercing night, and unnatural darkness. Because it draws above almost everything, it may also " +
            "render over some overlays (weapons, stun text, weather effects etc), which some players find jarring. " +
            "Rarely, it may also misalign during heavy animation or if the pawn moves between game ticks.",
            disabled: !anyReactorRendered);

        listing.Gap(6f);
        CheckboxLabeled(listing, "Scale reactor glow opacity by power need level",
            ref scaleReactorGlowByPower,
            "When enabled, the reactor core glows at full strength when the power need meter is full, and dims " +
            "as the level drops. When disabled, the glow stays at a constant full brightness.",
            disabled: !anyReactorRendered);

        listing.Gap(30f);
    }
}
