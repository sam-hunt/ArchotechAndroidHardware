---
name: translate
description: Generate, update, or audit mod localization (Keyed and DefInjected) for a target language, grounded in vanilla RimWorld terminology, Vanilla Races Expanded - Android's (VREA) own English strings, and Biotech's xenotype/gene vocabulary. Use when asked to add a language, update translations, or check translation freshness.
argument-hint: "[language, e.g. German | update | check]"
---

# Translate

Produce or refresh localization files for Archotech Android Hardware. English
is the source of truth; every other language derives from it.

**The family-wide process lives in the `l10n/` submodule — load these first,
and only these** (progressive disclosure; if `l10n/` is empty, run
`git submodule update --init`):

- `l10n/process.md` — non-negotiables, file/format conventions, terminology
  grounding method, and the generation / update / audit workflows. This is
  the workflow authority; follow it step by step.
- `l10n/languages/<Language>.md` — the target language's engine mechanics,
  style rules, and vanilla-grounded common vocabulary. Read ONLY the target
  language's file.
- `glossary/<Language>.md` (beside this file) — this mod's own coined-term
  glossary for the target language. Read it in the same pass.
- `l10n/lessons.md` — cross-language lessons; read when generating a new
  language, skim otherwise.
- `l10n/workshop.md` — Steam Workshop description/title conventions;
  `AAH_SettingsCategory` is this mod's title-coupling key.

**Where learnings land:** mod-independent findings (engine mechanics, a
language's grammar rule, corpus style facts) go in the `l10n/` submodule —
edit the canonical checkout at `~/dev/rimworld-l10n`, commit and tag there.
Mod-specific findings (coined terms, phrasing decisions) go in
`glossary/<Language>.md`.

**Before any pass, bump the pin:** run `l10n/tools/bump-consumer.sh` (fetches
upstream's release tags, checks out the latest, commits the pointer as `chore:
Bump l10n submodule vOLD -> vNEW`; no-op when already current). This is one of
the three moments a pin moves (release, pass start, new upstream major), never
per upstream commit. If it reports a MAJOR bump, read the upstream release
notes for the shim or flow edit this repo owes before continuing.

## This mod's translation surface

- English Keyed source: `1.6/Languages/English/Keyed/AAH_UI.xml` — the mod
  settings window's section headers, checkbox labels/descriptions, slider
  value templates and status tags. Every key is `AAH_`-prefixed. There is no
  second Keyed file.
- **This mod ships a real DefInjected surface**, not just XML patches on
  vanilla defs: labels and descriptions on its own `HediffDef`s, `GeneDef`s,
  `ThingDef`s, `RecipeDef`s, `ThoughtDef`s, `ScenarioDef`s, `ScenPartDef`s
  and one `RulePackDef` (the battle-log entry for a Thanatic reactor kill),
  all under `1.6/Defs/`. The authority for what needs translating is never a
  hand-maintained list, it's `Scripts/expected-injections.json`, regenerated
  by `Scripts/refresh-translation-expectations.py` — some translatable
  fields (inherited labels, comp-default strings, vanilla/VREA base-def
  fields reached only through a runtime-inherited parent) exist without ever
  appearing in this repo's own XML at all.
- **Subclass def type alias:** VREA's `AndroidGeneDef` companion genes are
  declared via that subclass but dumped under the base `GeneDef` folder — the
  checker's `DEF_TYPE_ALIASES` config encodes this; DefInjected entries for
  those genes go under `DefInjected/GeneDef/`, not a namespace-qualified
  folder. This mod defines no custom Def *subclasses* of its own, so no
  other def type needs a namespace-qualified folder today.
- DefInjected keys are `DefName.field` paths, e.g.
  `AAH_ThanaticReactor.label`. This mod's companion-part convention makes the
  same defName do triple duty across a `HediffDef`, a `GeneDef` and a
  `ThingDef` (see CLAUDE.md's "Def Naming Convention") — that's a naming
  coincidence at the C# `AAH_DefOf` layer, not a merge at the DefInjected
  layer: each def *type* still gets its own folder and its own independent
  key for the same defName.
- **No gated compat load root exists yet, but this mod will need one the day
  any of its MayRequire-gated defs get translations.** The grav reactor's
  `GeneDef`/`HediffDef`/`ThingDef`/`RecipeDef`s and its mote are
  Odyssey-gated, `AAH_MakeThanaticReactor` (`RecipeDef`) is Anomaly-gated,
  and `AAH_SalvageThanaticReactor` (`RecipeDef`) is VFEPower-gated (a
  workshop mod, not a DLC). MayRequire is ignored on DefInjected entries, so
  each gate must become a LoadFolders-gated folder — e.g.
  `1.6/Mods/Odyssey/Languages/<Language>/...` — the moment translations for
  that content are added, mirroring `BetterTradersGuild`'s `1.6/Mods/Biotech`
  pattern. Never add those entries to the main `1.6` tree: that loads
  unconditionally and is a found-no-def startup error whenever the gate is
  inactive. See TODOs.md for the tracked follow-up.

## This mod's grounding domain

Domain DLC: **Biotech** (hard dependency; also the expansion where every
xenotype/gene term in vanilla's own localization lives — this mod's
companion genes, biostatMet-driven drain rates, and xenogene lifecycle all
borrow that vocabulary), plus **Odyssey** for the grav reactor specifically
(gravship launch/landing terms, MayRequire-gated) and **Anomaly** for the
Thanatic reactor's crafting recipe. Royalty and Ideology don't matter here.

**Vanilla Races Expanded - Android's own English strings are a second
grounding source**, and not optional: VREA is a hard dependency (not an
optional DLC) and every AAH part either overrides or sits beside its
vocabulary — android, awakened, xenotype, subroutine, reactor / power need,
behavior station, reprogramming, core component. VREA ships English only
(`$RIMWORLD_PATH/../../workshop/content/294100/2975771801/Languages/English/Keyed/Keys.xml`,
plus its Defs' own label/description text) — there is no other-language VREA
data to cross-check against, so these terms must be coined consistently
across this mod's own translations rather than grounded a second time.

Terms that MUST be grounded before use: android, awakened, xenotype, gene,
archite gene, gene complexity, metabolism (the gene stat that drives this
mod's Thanatic/Grav reactor drain-rate genes, not the pawn need), subroutine,
core component, reprogramming/behavior station, inspiration, psychic
sensitivity — plus this mod's own coined vocabulary that has no vanilla
equivalent to borrow (vanometric/thanatic/grav reactor, overcharge, psychic
transceiver, archotech mnemocore, neutrosynthesizer, self-determination),
for which coining must stay internally consistent across every Keyed/
DefInjected entry that restates it.

**No language has run a generation pass yet** (English-only mod today), so
every `glossary/<Language>.md` file records only what to ground first, not
grounded answers. The first pass to add a language records its terms there
per `l10n/process.md`.

This mod's one `RulePackDef` (the Thanatic-kill battle-log entry) has no
name-grammar of its own — it uses only the standard SUBJECT/INITIATOR
grammar slots shared with vanilla `Event_*` packs. The weapon-mod family's
RulePackDef-specific techniques (namer-list gender/case marking,
`traitAdjectives` shape rules, and the rest) do not apply here.

## Workflows

Follow `l10n/process.md`'s Initial generation / Update pass / Audit-only
workflows verbatim. This mod's specifics on top:

- The checker: `python3 Scripts/check-translations.py` (`--strict` for new
  languages). Sidecar regen: `python3
  Scripts/refresh-translation-expectations.py` (game must be closed; drives
  the deployed L10nProbe).
- Enumerate the target key set from `AAH_UI.xml` plus every `required`
  DefInjected entry in the `Scripts/expected-injections.json` sidecar, NOT
  from scanning `1.6/Defs/` yourself — the sidecar sees inherited/comp-default
  text a hand scan can't.
- Compat-root routing per the surface section above; the checker's
  missing-entry errors name the root a translation belongs under.
- The public roster (and credits) is CONTRIBUTING.md's localization table —
  update it in the same commit as any language addition or native review.
