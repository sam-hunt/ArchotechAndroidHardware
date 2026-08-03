# TODO

> **Next session — infra modernization queued.** Follow the spec at
> `docs/Specs/Infra-Modernization.md` (written 2026-08-03 from the TradersStockXenogerms port session;
> TSX is the freshest exemplar). Infra only — no translation generation.

## Features

- Grav Reactor/installed-corpse destruction should respawn grav core
- Scale grav refill fraction + Grav Overcharge hours by gravship trip distance
  (deferred from the Grav Overcharge build; straight-line tiles between the
  controller's takeoff/landing tiles, normalization curve + reference-distance knob)

- Add Persona Core upgrade part (increase consciosness +10%, increase threshold to awaken?)

- Add setting-gated workgiver to autoqueue reactor replacements on low-power androids?
- Add setting sliders for low-power alert/auto-replacement thresholds? PR upstream?

- Eject Thanatic reactor on corpse destruction?
- Extract Thanatic reactor gizmo on corpse.
  `claude --resume "thanatic-reactor-designation-ui"`

- Magus of the Library scenario
  - Vanometric reactor
  - Green armor
  - Archotech arm
  - 7 books
  - Custom backstory

- Verify Magus of the Abyss in the UI (scenpart defs?)
  - Set custom backstory

- Only fire/scale thanatic drain on psychically sensitive targets
- Add powerfocus chip to reactor recipes? Evaluate recipe costs
- more exclusion tags for conflicting subroutines
- When sleep mode is used by an android with a vanometric reactor installed, building acts as a 500w power emitter

## Other?

- Android stands aren't pawn-bound and androids with low memory will path to and use the nearest

## Cleanup

- Review def descriptions copy
- Keyed language strings everywhere
- Check patches for upstream issues and file reports/PRs
- Neutro infusion operation upstream feature PR?
