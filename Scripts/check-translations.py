#!/usr/bin/env python3
# Archotech Android Hardware's config shim over the shared translation
# checker (l10n/checker/check_translations.py — the rimworld-l10n
# submodule). The engine holds all logic; this file holds only this repo's
# config and the rationale behind it. Usage is unchanged:
#   python3 Scripts/check-translations.py [--strict] [--root PATH]
# If l10n/ is empty, run: git submodule update --init

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "l10n" / "checker"))
import check_translations as engine  # noqa: E402  (import after sys.path edit)

engine.REPO_ROOT = Path(__file__).resolve().parent.parent

# PARITY_EXEMPT_FIELDS stays empty. The one matching-token field here (ScenPart_StartingBodyPart.bodyPartLabel,
# compared against BodyPartRecord.untranslatedCustomLabel) is handled at the
# source with [NoTranslate], which drops it from the sidecar and makes the game
# refuse a DefInjected override for it. PARITY_EXEMPT_FIELDS only excuses a
# secondary field from cross-language parity, the opposite semantics.
engine.PARITY_EXEMPT_FIELDS = set()

# RATIONALE: Biotech is a hard dependency (About.xml's modDependencies) —
# without it none of this mod's defs load at all. Odyssey is not a hard
# dependency but does gate content via MayRequire (the grav reactor's
# ThingDef/RecipeDef, per AAH_BodyPartAndroidArchotechBase's grav-reactor
# exception and its crafting recipe's gravcore-cell ingredient) — without
# Odyssey active, the grav reactor's labels/descriptions drop out of the
# sidecar and any already-shipped translations for them turn illegal.
# Anomaly likewise gates the Thanatic reactor's shard-based crafting recipe
# (AAH_MakeThanaticReactor). VanillaExpanded.VFEPower is also MayRequire-
# gated (the Thanatic salvage recipe), but it is a workshop mod, not a DLC,
# so it cannot appear in activeDlcs — it is handled instead by
# refresh-translation-expectations.py's CANONICAL_ACTIVE_MODS, which pins
# every mod (not just DLC) the probe boots with.
engine.REQUIRED_DLCS = {"Biotech", "Anomaly", "Odyssey"}

# Def XML may declare a def via a subclass the game rolls into a base-type
# database — the probe's walker (the game's own) then dumps those defs
# under the base type, and DefInjected translations legally target that
# base-type folder.
# RATIONALE: VREA's AndroidGeneDef companion genes are dumped under GeneDef.
engine.DEF_TYPE_ALIASES = {
    "VREAndroids.AndroidGeneDef": "GeneDef",
}

# This mod ships a real Keyed surface, so a missing Languages/ tree is a
# hard config error, not a legal state.
engine.ALLOW_NO_KEYED_SURFACE = False

# The localized Steam Workshop title lives in this Keyed key (the
# settings-window header); the checker enforces the title-coupling rule
# against each .steamworkshop/Description/<Language>.txt title line.
engine.WORKSHOP_TITLE_KEY = "AAH_SettingsCategory"

raise SystemExit(engine.main())
