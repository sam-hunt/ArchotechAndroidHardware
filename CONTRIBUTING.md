# Contributing

Thanks for your interest in improving Archotech Android Hardware! Bug reports,
suggestions and pull requests are welcome.

## Localization

| Language | Status | Credit |
| -------- | ------ | ------ |
| English  | Source | —      |

Translations for any language RimWorld supports are welcome. See "Contributing
a translation" below for the conventions to follow.

Statuses: **Source** (the authoritative English strings), **Machine-assisted**
(generated with terminology grounded against the official RimWorld
localization, Vanilla Races Expanded - Android's own English strings, and
Biotech's gene/xenotype vocabulary; awaiting native review), **Native**
(written or reviewed by a native speaker), **Planned** (not started —
contributions welcome).

### Contributing a translation

- Files live under `1.6/Languages/<Language>/` (`Keyed/` and `DefInjected/`),
  mirroring the structure of `1.6/Languages/English/`.
- Every translated entry carries the current English source in a comment
  directly above it, e.g. `<!-- EN: Reset to defaults -->` — this is how stale
  translations are detected when the English changes.
- Placeholders (`{0}`, `{1}`, ...) must match the English exactly.
- English Keyed source: `1.6/Languages/English/Keyed/AAH_UI.xml` — the mod
  settings window's labels, descriptions and value tags.
- This mod's DefInjected surface is real (unlike some sibling repos) — labels
  and descriptions on its `HediffDef`s, `GeneDef`s, `ThingDef`s, `RecipeDef`s,
  `ThoughtDef`s, `ScenarioDef`s, `ScenPartDef`s and `RulePackDef`s under
  `1.6/Defs/`.
- Formatting: UTF-8 without BOM, LF line endings, 2-space indent.
- Validate before opening a PR:

  ```bash
  python3 Scripts/check-translations.py --strict
  ```

  It checks key coverage, placeholders, DefInjected paths, staleness, and
  file hygiene. The checker's engine lives in the `l10n/` git submodule, so
  clone with `git clone --recurse-submodules` (or run
  `git submodule update --init` in an existing clone) before validating.

- Improving a machine-assisted language? Corrections from native speakers
  are gladly merged, no matter how small.
