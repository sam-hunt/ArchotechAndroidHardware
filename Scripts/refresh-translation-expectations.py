#!/usr/bin/env python3
# Regenerates Scripts/expected-injections.json — the checked-in "sidecar" of
# every DefInjected key the live game expects for this mod — by driving the
# L10nProbe dev mod (../L10nProbe; must be deployed, active last in the mod
# list, and configured with this mod's packageId).
#
# The sidecar is what makes unknown localization-gap classes impossible by
# structure: check-translations.py validates languages against it and refuses
# to run against stale expectations (any defName in Defs/ it has never seen),
# so every content change forces a regen, and the regen sees everything the
# game sees (vanilla-inherited fields, C# defaults) via the game's own walker,
# Verse.DefInjectionUtility.ForEachPossibleDefInjection.
#
# Flow: launch the game with -l10nprobe (graphical boot, ~1-2 min; the probe
# runs once defs are loaded, writes one JSON per configured mod, then quits
# the game), fetch this mod's dump from the probe's Output folder, rewrite the
# sidecar, print a key-level diff summary.
#
# The probe boots with a PINNED mod list (CANONICAL_ACTIVE_MODS below), not
# whatever the user last played with: ModsConfig.xml is swapped for the run
# and restored afterwards. The dump reflects the LIVE def graph, so any
# third-party mod that patches our defs leaks into the expectations — a mod
# that reshuffles our stock generators or hediff comps would show up in the
# dump even though it is not installed for most players. The pinned list is
# this mod + hard deps + every DLC our defs can MayRequire, matching the
# configuration the sidecar was first verified against.
#
# Operational facts (verified against the probe's SPEC + live runs, 2026-07-31):
#   * The game's exit code is 0 even when probing fails — success is judged by
#     the output file, never the exit code.
#   * On per-mod failure the probe guarantees NO output file at that mod's
#     path (a pre-existing one is deleted). We delete the file before
#     launching anyway, so a leftover from an older run can't be mistaken for
#     a fresh result.
#   * meta.generated is the only field that changes between runs on identical
#     content, so it is stripped from the sidecar: an unchanged repo
#     regenerates to a byte-identical file and "git diff is empty" means
#     "nothing changed". meta.gameBuild is kept — a diff in it explains why
#     keys moved when no local def changed (vanilla update).
#
# Usage:
#   python3 Scripts/refresh-translation-expectations.py            # launch + refresh
#   python3 Scripts/refresh-translation-expectations.py --no-launch  # reuse the
#     dump already in the probe's Output folder (debugging / probe just ran)

import argparse
import json
import os
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

# Per-repo constants. This script's structure mirrors the copy in
# shunter.uniquemeleeweapons and its sibling repos (same L10nProbe-driven
# refresh flow); unlike that trio, this mod is not part of a probed family —
# it is the only sidecar-bearing mod in its own boot. Unlike
# shunter.tradersstockxenogerms (which has no MayRequire-gated content and no
# hard third-party dependency beyond Harmony), this mod hard-depends on a
# third-party mod (VREA) and MayRequire-gates content behind both a DLC
# (Odyssey) and a second third-party mod (VFEPower) — see
# CANONICAL_ACTIVE_MODS below.
PACKAGE_ID = "shunter.archotechandroidhardware"

ROOT = Path(__file__).resolve().parent.parent
SIDECAR = ROOT / "Scripts" / "expected-injections.json"


# The exact list the probe boots with — deterministic expectations need a
# deterministic def graph. Order is load order; the probe must be last.
# Membership rule:
#   * Core, plus every DLC this mod hard-requires OR gates content behind via
#     MayRequire — a def whose gate is absent never loads, so its keys drop
#     out of the sidecar and its already-shipped translations turn illegal.
#     Biotech is a hard dependency (About.xml's modDependencies); Odyssey
#     gates the grav reactor (its ThingDef/RecipeDef carry MayRequire —
#     "ludeon.rimworld.odyssey" per the grav reactor's crafting recipe and
#     About.xml's loadAfter); no other DLC gates any content here;
#   * hard dependencies: Harmony, plus Vanilla Races Expanded - Android
#     (vanillaracesexpanded.android) — every AAH_ hediff/gene/ThingDef parents
#     a VREA def at runtime (see CLAUDE.md's "VREA Parent Defs"), so nothing
#     in this mod loads without it;
#   * a third-party mod ONLY if this mod's content MayRequires it.
#     VanillaExpanded.VFEPower gates the Thanatic reactor's crafting recipe
#     only (violence-generator ingredient) — the hediff/gene/ThingDef/surgery
#     recipes all load without it, but the crafting RecipeDef's own
#     label/description (if any) would vanish from a dump made without it;
#   * this repo's own sidecar-bearing mod. Unlike the UniqueMeleeWeapons
#     family (three independent mods riding along in one boot for
#     convenience), this mod is not part of a probed family, so the list has
#     no siblings to add;
#   * transitive dependencies the boot needs to be sound: Vanilla Expanded
#     Framework (OskarPotocki.VanillaFactionsExpanded.Core) is a hard
#     dependency of BOTH VREA and VFEPower — without it they load broken and
#     the def graph the probe walks is not the one players see.
# Nothing else. The probe filters each dump by packageId, so an extra mod
# adds no keys of its own — but its patches to OUR defs leak straight into
# the expectations (see header).
# Forgetting this list fails loudly, not silently: L10nProbe's settings UI
# reminds about it whenever a mod is ticked for probing, and launch_probe
# warns when a ticked mod is absent here (its dump cannot succeed).
# All ids LOWERCASE — the format RimWorld itself writes to ModsConfig.xml.
# This is load-bearing, not cosmetic: mod *loading* tolerates mixed case, but
# the MayRequire active-check is case-exact against the ModsConfig strings, so
# a mixed-case entry here loads the mod yet silently drops every def gated on
# it (observed 2026-08-03: VanillaExpanded.VFEPower loaded fine while both
# VFEPower-gated RecipeDefs vanished from the dump).
CANONICAL_ACTIVE_MODS = [
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


def rimworld_path():
    rw = os.environ.get("RIMWORLD_PATH")
    if not rw:
        sys.exit("RIMWORLD_PATH is not set (see CLAUDE.md's WSL setup note)")
    rw = Path(rw)
    if not (rw / "RimWorldWin64.exe").is_file():
        sys.exit(f"No RimWorldWin64.exe under RIMWORLD_PATH ({rw})")
    if not (rw / "Mods" / "L10nProbe" / "About").is_dir():
        sys.exit(f"L10nProbe is not deployed under {rw / 'Mods'} — build "
                 f"../L10nProbe first")
    return rw


def modsconfig_path():
    configs = sorted(Path("/mnt/c/Users").glob(
        "*/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios"
        "/Config/ModsConfig.xml"))
    if not configs:
        sys.exit("No ModsConfig.xml found under /mnt/c/Users — cannot pin "
                 "the probe mod list")
    return configs[-1]


def pinned_modsconfig(original_text):
    # Rebuild only <activeMods>; version and knownExpansions pass through.
    root = ET.fromstring(original_text)
    active = root.find("activeMods")
    for li in list(active):
        active.remove(li)
    for pid in CANONICAL_ACTIVE_MODS:
        ET.SubElement(active, "li").text = pid
    ET.indent(root, space="  ")
    return ET.tostring(root, encoding="unicode", xml_declaration=True) + "\n"


def game_is_running():
    # The probe boot needs the client to itself: a second instance fights
    # over the session, and the ModsConfig swap must not race a live game
    # (which rewrites the file on mod-list edits and on exit). tasklist.exe
    # is reachable from WSL via interop; if it is not, skip the check rather
    # than block the flow.
    try:
        out = subprocess.run(
            ["tasklist.exe", "/FI", "IMAGENAME eq RimWorldWin64.exe"],
            capture_output=True, text=True, timeout=15).stdout
    except (OSError, subprocess.TimeoutExpired):
        return False
    return "RimWorldWin64.exe" in out


def warn_unpinned_probe_targets():
    # Ticking a mod in L10nProbe's settings is the memorable onboarding step;
    # adding it to CANONICAL_ACTIVE_MODS above is the forgettable one. Catch
    # the gap before the boot: a ticked mod absent from the pinned list is
    # simply not loaded during the run, so its dump is guaranteed to fail.
    cfg = modsconfig_path().parent / "Mod_L10nProbe_L10nProbeMod.xml"
    if not cfg.is_file():
        return
    try:
        node = ET.parse(cfg).getroot().find(".//targetPackageIds")
    except ET.ParseError:
        return
    pinned = set(CANONICAL_ACTIVE_MODS)
    for li in node if node is not None else []:
        pid = (li.text or "").strip().lower()
        if pid and pid not in pinned:
            print(f"warning: {pid} is ticked for probing in L10nProbe's "
                  f"settings but is not in CANONICAL_ACTIVE_MODS — it will "
                  f"not be loaded during this boot and its dump WILL fail",
                  file=sys.stderr)


def launch_probe(rw, dump_path):
    if game_is_running():
        sys.exit("RimWorld is already running — the probe needs an exclusive "
                 "boot (mod-list swap + -l10nprobe). Close the client, then "
                 "rerun this script.")
    warn_unpinned_probe_targets()
    dump_path.unlink(missing_ok=True)
    mc = modsconfig_path()
    original = mc.read_bytes()
    mc.write_text(pinned_modsconfig(original.decode("utf-8-sig")),
                  encoding="utf-8")
    print("Launching RimWorld with -l10nprobe on the pinned mod list "
          "(graphical boot, ~1-2 min)...")
    try:
        # WSL interop blocks until the Windows process exits; the probe
        # quits the game itself after writing its dumps.
        subprocess.run(["./RimWorldWin64.exe", "-l10nprobe"], cwd=rw,
                       check=False)
    finally:
        # Always hand the player's own mod list back, byte-identical.
        mc.write_bytes(original)
        print(f"Restored {mc}")


def load_dump(dump_path):
    if not dump_path.is_file():
        sys.exit(f"Probe wrote no dump at {dump_path} — absence of the file "
                 f"IS the failure marker; check Player.log for a "
                 f"'[L10nProbe] FAILED probing' line")
    dump = json.loads(dump_path.read_text(encoding="utf-8"))
    if dump.get("meta", {}).get("modPackageId") != PACKAGE_ID:
        sys.exit(f"{dump_path} is not a {PACKAGE_ID} dump")
    dump["meta"].pop("generated", None)
    # Record the boot's mod list so the checker can resolve def-level
    # MayRequire gates against what was actually active during the probe
    # (the probe itself only records DLCs).
    dump["meta"]["activeMods"] = CANONICAL_ACTIVE_MODS
    return dump


def key_set(dump):
    return {(dt, k) for dt, keys in dump["defInjections"].items() for k in keys}


def summarize(old, new):
    if old is None:
        print(f"Sidecar created: {sum(len(k) for k in new['defInjections'].values())} "
              f"entries across {len(new['defInjections'])} def types")
        return
    if old["meta"].get("gameBuild") != new["meta"].get("gameBuild"):
        print(f"gameBuild: {old['meta'].get('gameBuild')} -> "
              f"{new['meta'].get('gameBuild')} (vanilla-sourced English may "
              f"have changed even where no local def did)")
    if old["meta"].get("activeDlcs") != new["meta"].get("activeDlcs"):
        print(f"activeDlcs: {old['meta'].get('activeDlcs')} -> "
              f"{new['meta'].get('activeDlcs')}")
    old_keys, new_keys = key_set(old), key_set(new)
    for label, keys in (("added", new_keys - old_keys),
                        ("removed", old_keys - new_keys)):
        for dt, k in sorted(keys):
            print(f"  {label}: {dt}/{k}")
    changed = [(dt, k) for dt, k in sorted(old_keys & new_keys)
               if old["defInjections"][dt][k] != new["defInjections"][dt][k]]
    for dt, k in changed:
        print(f"  changed: {dt}/{k}")
    if old == new:
        print("No changes — sidecar is byte-identical.")
    else:
        print(f"{len(new_keys - old_keys)} added, {len(old_keys - new_keys)} "
              f"removed, {len(changed)} changed. New keys need translating in "
              f"every language (/translate) before check-translations.py "
              f"passes.")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--no-launch", action="store_true",
                    help="skip launching the game; consume the dump already "
                         "in the probe's Output folder")
    args = ap.parse_args()

    rw = rimworld_path()
    dump_path = rw / "Mods" / "L10nProbe" / "Output" / f"{PACKAGE_ID}.json"
    if not args.no_launch:
        launch_probe(rw, dump_path)
    new = load_dump(dump_path)

    old = json.loads(SIDECAR.read_text(encoding="utf-8")) \
        if SIDECAR.is_file() else None
    # Match the probe's own formatting (two-space indent, LF, UTF-8 no BOM,
    # trailing newline) so the sidecar diffs cleanly against raw dumps.
    SIDECAR.write_text(json.dumps(new, indent=2, ensure_ascii=False) + "\n",
                       encoding="utf-8")
    summarize(old, new)
    print(f"Wrote {SIDECAR.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
