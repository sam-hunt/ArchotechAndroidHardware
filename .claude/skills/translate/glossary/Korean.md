# Korean — Archotech Android Hardware glossary

No generation pass has run for this language yet (English-only mod today).
Family-shared mechanics (LanguageWorker behavior, style/corpus rules,
vanilla-grounded common vocabulary, and the josa-particle-marker rules) live
in the `l10n/` submodule at `l10n/languages/Korean.md` — this file will hold
only what's specific to Archotech Android Hardware once a pass grounds it.

## Terms to ground first

Vanilla/VREA vocabulary that must be grounded against the Biotech tar and
VREA's own English strings before use: android, awakened, xenotype, gene,
archite gene, gene complexity, metabolism (the gene stat this mod's
Thanatic/Grav reactor drain-rate genes use, not the pawn need), subroutine,
core component, reprogramming/behavior station, inspiration, psychic
sensitivity.

This mod's own coined vocabulary has no vanilla equivalent to borrow, so it
must be coined here and then used consistently across every Keyed/
DefInjected entry that restates it: vanometric/thanatic/grav reactor,
overcharge, psychic transceiver, archotech mnemocore, neutrosynthesizer,
self-determination. Every josa particle following one of these coined terms
still needs a marker per the shared engine rule — ground the term first, then
mark it.

## Out of scope

This mod's one `RulePackDef` (the Thanatic-kill battle-log entry) uses only
the standard SUBJECT/INITIATOR grammar slots shared with vanilla `Event_*`
packs — no name-grammar composition of its own. The weapon-mod family's
`RulePackDef`-specific glossary rows (weapon/tool/damage vocabulary, the
mod-decided trait-adjective list, trader/market-value phrasing) belong to a
different domain and don't apply here; see `../UniqueMeleeWeapons` or
`../XenogermTraderStock`'s skill if that ever changes.
