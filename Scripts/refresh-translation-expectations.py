#!/usr/bin/env python3
# Archotech Android Hardware's config shim over the shared sidecar-refresh
# engine (l10n/refresh/refresh_expectations.py — the rimworld-l10n
# submodule), which drives the L10nProbe dev mod (source at l10n/probe/;
# build/deploy it only from the canonical ~/dev/rimworld-l10n checkout). The
# engine holds all logic; this file holds only this repo's config and the
# rationale behind it. Usage is unchanged (game must be closed):
#   python3 Scripts/refresh-translation-expectations.py [--no-launch]
# If l10n/ is empty, run: git submodule update --init

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "l10n" / "refresh"))
import refresh_expectations as engine  # noqa: E402  (import after sys.path edit)

engine.REPO_ROOT = Path(__file__).resolve().parent.parent

engine.PACKAGE_ID = "shunter.archotechandroidhardware"

# RATIONALE: Biotech is a hard dependency (About.xml's modDependencies);
# Odyssey gates the grav reactor (its ThingDef/RecipeDef carry MayRequire —
# "ludeon.rimworld.odyssey" per the grav reactor's crafting recipe and
# About.xml's loadAfter); Anomaly gates the Thanatic reactor's shard-based
# crafting recipe (AAH_MakeThanaticReactor). Hard third-party dependency:
# Vanilla Races Expanded - Android (vanillaracesexpanded.android) — every
# AAH_ hediff/gene/ThingDef parents a VREA def at runtime (see CLAUDE.md's
# "VREA Parent Defs"), so nothing in this mod loads without it.
# VanillaExpanded.VFEPower is MayRequire-gated too (the Thanatic salvage
# recipe's violence-generator ingredient). Vanilla Expanded Framework
# (OskarPotocki.VanillaFactionsExpanded.Core) rides along as a transitive
# dependency the boot needs to be sound: it is a hard dependency of BOTH
# VREA and VFEPower, so without it they load broken and the def graph the
# probe walks is not the one players see. This mod is not part of a probed
# family, so the list has no sibling packageIds to add. See the engine's
# header for the general membership rule and the lowercase-id warning;
# order is load order, the probe last.
engine.CANONICAL_ACTIVE_MODS = [
    "brrainz.harmony",
    "ludeon.rimworld",
    "ludeon.rimworld.biotech",
    "ludeon.rimworld.anomaly",
    "ludeon.rimworld.odyssey",
    "oskarpotocki.vanillafactionsexpanded.core",
    "vanillaracesexpanded.android",
    "vanillaexpanded.vfepower",
    "shunter.archotechandroidhardware",
    "shunter.l10nprobe",
]

raise SystemExit(engine.main())
