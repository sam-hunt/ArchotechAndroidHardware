#!/usr/bin/env python3
# Pre-release integration smoke test: boots the real game once with AAH plus
# VREA, VEF and VFE Power - the seam this mod's 10 Harmony patches actually
# target - on a pinned minimal list where the baseline is a clean log, then
# classifies every Player.log error/warning by origin and fails on anything
# attributed to AAH or an integration seam. Thin shim over the shared engine
# in l10n/smoke/startup_smoke.py (see its header for mechanics and the BTG
# v1.1.0 CWTL incident this exists to catch).
#
# Run this before every release, with the game closed:
#   python3 Scripts/integration-smoke-test.py              # boot + scan
#   python3 Scripts/integration-smoke-test.py --no-launch  # rescan last log
#   python3 Scripts/integration-smoke-test.py --strict     # any error fails

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "l10n" / "smoke"))
import startup_smoke as engine  # noqa: E402

engine.REPO_ROOT = Path(__file__).resolve().parent.parent

engine.PACKAGE_ID = "shunter.archotechandroidhardware"

# RATIONALE: this is this repo's l10n CANONICAL_ACTIVE_MODS list. VREA is the
# hard dependency this mod exists for - all 10 Harmony patches target it
# (android reactor gates, the behaviorist station dialog, the create/modify
# dialog, the corpse inspect-string fixup). VEF is VREA's own hard dep and
# must load before it. VFE Power is MayRequire-gated (thanatic reactor
# salvage recipe). Probe last (auto-quit).
engine.SMOKE_ACTIVE_MODS = [
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

engine.OWN_PATTERNS = ["ArchotechAndroidHardware", "AAH_"]

# The other mod's namespaces/prefixes: an error mentioning any of these gates
# the test even when the exception fires inside their code - the same class
# of failure as the v1.1.0 BTG/CWTL incident, which surfaced as a red error
# inside CWTL's own static ctor.
engine.INTEGRATION_PATTERNS = {
    "VREA": ["VREAndroids"],
    "VEF": ["VEF.", "PipeSystem"],
    "VFEPower": ["VanillaPowerExpanded"],
}

raise SystemExit(engine.main())
