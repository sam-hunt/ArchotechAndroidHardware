#!/usr/bin/env bash
#
# Post-turn mod sync (Claude Code Stop hook helper). Tracked and identical across the
# mod family; wired by the tracked .claude/settings.json. Repo-specific values (solution,
# project folder, mod name) are derived below, so the file needs no per-repo edits.
#
# This script does NOT know the file manifest: the .csproj's StageMod target owns
# "what files ship" and does the atomic wipe+recopy deploy. This script only decides
# *whether* to build this turn and surfaces failures instead of swallowing them:
#
#   * Skips silently when no mod-relevant source/content changed since the last deploy,
#     and when no RimWorld install is present (CI, headless, a contributor's machine).
#   * On build/deploy failure, exits 2 with the errors on stderr: Claude Code feeds that
#     back to the agent and keeps the turn going, so the agent fixes the build (warnings
#     are errors via TreatWarningsAsErrors) instead of the failure landing on the user.
#   * If the turn is already a continuation forced by this hook (stop_hook_active in the
#     payload) it prints the warning to the user and exits 0 instead, so a build the agent
#     cannot fix never loops.
#
# Usable standalone too:  ./.claude/hooks/sync-mod.sh
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Repo root via git (robust no matter how deep this helper is nested), with a fallback.
REPO="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null)"
[ -n "$REPO" ] || REPO="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO" || exit 0

# Hook payload (JSON on stdin; absent when run standalone from a TTY). Only
# stop_hook_active matters: true means a previous run of this hook already blocked
# the stop once this turn, so block no further.
HOOK_INPUT=""
[ -t 0 ] || HOOK_INPUT="$(cat 2>/dev/null || true)"

# Build target: the single solution at the repo root (it also compiles any Tests
# project, so test-side warnings surface here too), else the first mod csproj.
TARGET="$(ls "$REPO"/*.sln 2>/dev/null | head -n1)"
PROJ="$(ls "$REPO"/Source/*/*.csproj 2>/dev/null | head -n1)"
[ -n "$TARGET" ] || TARGET="$PROJ"
[ -n "$TARGET" ] || exit 0                        # nothing to build

NAME="$(sed -n 's|.*<name>\(.*\)</name>.*|\1|p' About/About.xml 2>/dev/null | head -n1)"
[ -n "$NAME" ] || NAME="$(basename "$REPO")"
STAMP="$(dirname "$PROJ")/obj/.deploy-stamp"   # under obj/ (gitignored); survives normal builds
LOG="${TMPDIR:-/tmp}/$(basename "$REPO")-build.log"

# Nothing to deploy to without a RimWorld install (CI / headless): bail quietly.
DEFAULT_WIN_PATH="/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld"
if [ -z "${RIMWORLD_PATH:-}" ] && [ ! -d "$DEFAULT_WIN_PATH" ]; then
  exit 0
fi

# Has any mod-relevant input changed since the last successful deploy? Scope to
# source + content only (never obj/bin or build outputs, which would otherwise mark
# every build as "changed"). The content list mirrors StageMod's roots: the repo root,
# any version folder (*/) and the compat roots (Mods/, */Mods/).
changed=1
if [ -f "$STAMP" ]; then
  newer="$( {
    find Source Tests -type f \( -name '*.cs' -o -name '*.csproj' \) \
         -not -path '*/obj/*' -not -path '*/bin/*' -newer "$STAMP"
    find Defs Patches Languages Textures Sounds Mods About LoadFolders.xml \
         */Defs */Patches */Languages */Textures */Sounds */Mods \
         -type f -newer "$STAMP"
  } 2>/dev/null | head -n1 )"
  [ -z "$newer" ] && changed=0
fi

[ "$changed" -eq 0 ] && exit 0   # no mod files touched this turn: nothing to do

# Detach stdin (</dev/null) alongside redirecting stdout+stderr to the log: with no
# console on any standard stream, the dotnet CLI skips its startup console-encoding
# probe. That probe (Console.Input/OutputEncoding -> ICU GetCultureByName) can abort
# the process in some sandboxed/TTY-inheriting contexts; detaching makes it immune.
if dotnet build "$TARGET" -c Release --verbosity quiet </dev/null >"$LOG" 2>&1; then
  mkdir -p "$(dirname "$STAMP")" && : > "$STAMP"
else
  report() {
    echo "$NAME build/deploy FAILED after this turn. The game folder may hold a stale DLL."
    echo "  Full log: $LOG"
    echo "  --- errors ---"
    # Each error is logged once per project that compiles the file (a Tests project
    # recompiles the main csproj), so dedupe. Fall back to the log tail when the failure
    # is not a compiler/analyzer error (restore, deploy copy, ...).
    errs="$(grep -E ': error ' "$LOG" 2>/dev/null | sed "s|$REPO/||; s| \[.*||" | sort -u | head -n 20)"
    if [ -n "$errs" ]; then echo "$errs"; else tail -n 15 "$LOG" 2>/dev/null; fi
  }
  if printf '%s' "$HOOK_INPUT" | grep -q '"stop_hook_active": *true'; then
    report          # already blocked once this turn: inform the user, let the turn end
    exit 0
  fi
  { echo "Fix the build before stopping (warnings are errors)."; report; } >&2
  exit 2
fi
exit 0
