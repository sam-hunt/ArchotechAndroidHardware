# Glossary — Archotech Android Hardware-specific terminology

These per-language files (`Russian.md`, `Japanese.md`, `ChineseSimplified.md`,
`Korean.md`, `German.md`, `Spanish.md`, `French.md`,
`PortugueseBrazilian.md`) hold everything about a language's translation
that is specific to Archotech Android Hardware: mod-coined terms
(vanometric/thanatic/grav reactor, overcharge, psychic transceiver, archotech
mnemocore, neutrosynthesizer, self-determination, and the like), the
localized Workshop title (`AAH_SettingsCategory`), and any worked phrasing
decisions tied to specific `AAH_` defs, once a generation pass records them.

Family-shared, mod-independent findings — LanguageWorker mechanics, style
and corpus rules, and vanilla-grounded common vocabulary (android, xenotype,
gene, quality tiers, and so on) — live upstream in the `l10n/` submodule at
`l10n/languages/<Language>.md` (canonical checkout: `~/dev/rimworld-l10n`),
since they apply to any mod in the family, not just this one.

**No language has run a generation pass yet** — this is an English-only mod
today. Each file below records only what to ground before the first pass:
the must-ground vanilla/VREA terms, this mod's own coined vocabulary with no
vanilla equivalent, and why the weapon-mod siblings' `RulePackDef`
name-grammar techniques don't apply here. When a future pass coins a term,
record it here. If a pass instead surfaces a correction to shared mechanics
or vocabulary, send that fix upstream to the l10n repo rather than
duplicating it here.
