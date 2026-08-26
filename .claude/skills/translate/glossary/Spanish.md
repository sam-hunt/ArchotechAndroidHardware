# Spanish (Castellano) — Archotech Android Hardware glossary

No generation pass has run for this language yet (English-only mod today).
Family-shared mechanics (LanguageWorker behavior, style/corpus rules,
vanilla-grounded common vocabulary) live in the `l10n/` submodule at
`l10n/languages/Spanish.md` — this file will hold only what's specific to
Archotech Android Hardware once a pass grounds it.

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
self-determination. Watch for the `de el`→`del`/`a el`→`al` contraction rule
the shared engine file documents wherever a coined term follows `de`/`a`.

## Out of scope

This mod's one `RulePackDef` (the Thanatic-kill battle-log entry) uses only
the standard SUBJECT/INITIATOR grammar slots shared with vanilla `Event_*`
packs — no name-grammar composition of its own. The weapon-mod family's
`RulePackDef`-specific glossary rows (the `badass_concept`/`conceptF`
parallel-symbol-family technique, `traitAdjectives`/`namerLabels` shape
rules, quest-site vocabulary, trader/market-value phrasing) belong to a
different domain and don't apply here; see `../UniqueMeleeWeapons` or
`../XenogermTraderStock`'s skill if that ever changes.
