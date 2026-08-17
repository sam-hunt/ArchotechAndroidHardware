# .steamworkshop

Publishing metadata for the mod's Steam Workshop page. Nothing in this folder
ships with the mod (the StageMod manifest never matches it) or is loaded by
RimWorld. A `Media/` folder for Workshop images can live here later.

## Description/

One file per language, named after the RimWorld language folders in
`1.6/Languages/`. English is the source of truth; the others are
machine-assisted first passes pending native review. Format:

- Line 1: the Workshop title for that language
- Line 2: blank
- Rest: the BBCode description

Title convention: just as the English title leans on vanilla vocabulary
("archotech") plus the android vocabulary established by Vanilla Races
Expanded - Android (this mod's required companion) so players searching
either term find the mod, every localized title must contain that
language's vanilla-localized "archotech" term plus that language's android
vocabulary. Titles are fully localized with no English brand appended:
Workshop search is language-agnostic (any language's title matches
regardless of UI language, verified 2026-08-12) and the preview thumbnail
already carries the English name. Each title must equal that language's
`AAH_SettingsCategory` Keyed value so the in-game settings header matches
the Workshop page (see the CLAUDE.md localization note).

Steam has no API for per-language Workshop text, so updated files are pasted
manually into the Workshop page's edit UI (note Steam's own language names
differ: schinese, koreana, brazilian, latam, ...). The `release` skill diffs
`English.txt` against the last release tag and refreshes the translations
whenever it changed.

English is currently the only language: `1.6/Languages/` has no
non-English folders yet, so this folder holds only `Description/English.txt`.
Per-language description files arrive together with the initial translation
pass (tracked in TODOs.md).
