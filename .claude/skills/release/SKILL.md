---
name: release
description: Prepare and publish a versioned release — version bumps, changelog, build, commit, tag, push
disable-model-invocation: true
argument-hint: "[major|minor|patch]"
---

# Release

Prepare and publish a new release for Archotech Android Hardware.

The user may pass a bump type as `$ARGUMENTS` (one of `major`, `minor`, or `patch`). If omitted, ask which bump type they want (at step 6, where the version is first needed).

## Current state

!`git describe --tags --abbrev=0 2>/dev/null || echo "no tags found"`
!`git log "$(git describe --tags --abbrev=0 2>/dev/null || echo 'HEAD~10')..HEAD" --oneline --no-merges`

## Steps

Work through the steps below in order. Steps 1-5 are validation and may
generate their own commits, which is exactly why the release decision —
version, changelog, tag — happens once, at step 6, after everything that
still can change the history. Confirmations: the conditional translation
commits in steps 2-3 each get a diff review, and step 6 is the single
release gate; nothing else asks.

### 1. Review changes

The commit log since the last tag is shown above — read it now to understand
what this release contains. If the repo has no tags yet this is the first
release: use the full history (`git log --oneline --no-merges`) and think in
terms of the mod's shipped feature set rather than a diff. No confirmation —
this is orientation, not a decision.

### 2. Refresh translation expectations and check freshness

Run, in order:
```bash
l10n/tools/bump-consumer.sh   # pin l10n to its latest release tag (commits; no-op when current)
python3 Scripts/refresh-translation-expectations.py
python3 Scripts/check-translations.py --strict
```

- The first command is one of the three moments the l10n pin moves (release,
  translation-pass start, new upstream major); the checker's `--strict`
  engine-pin check fails until the pin is on the latest tag. It commits the
  pointer directly; report its one-line result. If it reports a **MAJOR**
  bump, stop: upstream changed the shim/flow contract and this repo owes the
  accompanying edit before the release continues.
- The refresh script refuses to start while RimWorld is already open (it
  needs an exclusive boot for the mod-list swap). If it reports that, **stop
  and ask the user** to close the client, and rerun only after they confirm
  it is free.
- The first command regenerates `Scripts/expected-injections.json` by
  launching the local RimWorld client with `-l10nprobe` (graphical boot,
  ~1-2 min; the L10nProbe dev mod dumps every DefInjected key the live game
  expects, then quits). This is what surfaces vanilla/VREA-inherited and
  C#-default strings a def-XML scan cannot see. Report its diff summary.
- If the diff shows **added or changed keys**, translate them in every
  language now (the `translate` skill's update pass), then rerun the checker.
- Report the per-language checker result (missing keys, stale entries,
  errors). CI's release gate runs the same script without `--strict` against
  the checked-in sidecar; the stricter local run surfaces warnings while
  there is still time to act on them.
- If the sidecar or any translations changed, commit them as their own
  `fix(l10n)` commit (show the diff and **ask the user to confirm**) before
  moving on — the release commit at step 7 stages only the version-bump
  files.

### 3. Refresh Steam Workshop page translations

The Workshop title and description live in
`.steamworkshop/Description/<Language>.txt` — line 1 is the title, then a
blank line, then the BBCode description; one file per language folder in
`1.6/Languages/`, English being the source of truth (see
`.steamworkshop/README.md`).

- Diff the English source against the last release:
  ```bash
  git diff $(git describe --tags --abbrev=0) -- .steamworkshop/Description/English.txt
  ```
- Also check for languages in `1.6/Languages/` with no description file yet.
- If nothing changed and no file is missing, say so and move on.
- Otherwise spawn one translation subagent per affected language (cheaper
  model, in parallel) to update or create its file, grounded in the
  `translate` skill's glossary section for that language and the mod's own
  committed `1.6/Languages/<Language>/` strings, preserving BBCode tags and
  the title-line format. Subagents never commit.
- Review the diffs, then commit them as their own `docs:` commit (show the
  diff and **ask the user to confirm**).

### 4. Clean build and deploy

Run:
```bash
dotnet clean ArchotechAndroidHardware.sln
dotnet build ArchotechAndroidHardware.sln -c Release
```

Report the build result. If the build fails, stop and help the user fix it.
On success, move straight to the smoke test — no confirmation.

### 5. Startup smoke test

Run (game closed — the script refuses while RimWorld is open, same as the
refresh in step 2; if it reports that, **stop and ask the user** to close the
client and rerun):

```bash
python3 Scripts/integration-smoke-test.py
```

- Boots the freshly deployed build once on a pinned list of AAH with VREA,
  VEF and VFE Power (graphical boot, ~1-2 min, auto-quits), then classifies
  every Player.log error by origin and fails on anything attributed to AAH
  or the VREA/VEF seam. This is the only automated coverage the VREA
  integration patches get; they never run on the normal dev modlist (see
  CLAUDE.md's Testing section).
- On PASS, report the summary line and move on — no confirmation. On FAIL,
  show the gated error blocks and **stop** — the release does not proceed
  until the errors are fixed or the user explicitly waives them. Third-party
  (`other`) errors are reported but not gating; mention them so the user can
  judge.

### 6. Version, changelog, and the single release confirmation

Everything that can change history has now run, so the release contents are
final. Do all of the following, then present it as **one** confirmation:

- Read the current version from `About/About.xml` (`<modVersion>`) and
  calculate the new version from the bump type (`$ARGUMENTS`, or ask now).
- Draft changelog notes from the full log since the last tag — including any
  commits steps 2-3 just created — grouped by category (Fixes, Features,
  Polish/Other), omitting chore/version-bump commits.
- Each changelog entry is a short one-liner fit for Steam Workshop change
  notes (see the note atop `CHANGELOG.md`).
- Update `CHANGELOG.md`: new `## [X.Y.Z] - YYYY-MM-DD` section at the top of
  the version list, directly below the Keep a Changelog intro paragraph,
  using today's date, Keep a Changelog style (`### Added`, `### Fixed`,
  ...). This changelog carries no `[Unreleased]` heading; don't add one.
  Also add a
  `[X.Y.Z]: https://github.com/sam-hunt/ArchotechAndroidHardware/releases/tag/vX.Y.Z`
  link reference at the bottom of the file, above any older ones.
- Bump the version string in all three files: `About/About.xml`
  (`<modVersion>`), `Source/1.6/Properties/AssemblyInfo.cs`
  (`AssemblyVersion` and `AssemblyFileVersion`, four-part `X.Y.Z.0`),
  `README.md` (version badge `Version-X.Y.Z`).
- Show the user, together: current version → new version (and bump type),
  the changelog notes, the full diff of all four files, and exactly what
  step 7 will do (rebuild, commit `chore: Bump version to X.Y.Z`, tag
  `vX.Y.Z`, push with tags).
- **Ask the user to confirm — this is the only release confirmation.** On
  edits, apply them and re-show only what changed.

### 7. Rebuild, commit, tag, push

No further questions unless something is unexpected:

- Rebuild:
  ```bash
  dotnet clean ArchotechAndroidHardware.sln
  dotnet build ArchotechAndroidHardware.sln -c Release
  ```
  so the deployed DLL carries the bumped `AssemblyVersion`. Stop on failure.
- Stage only the release files: `About/About.xml`,
  `Source/1.6/Properties/AssemblyInfo.cs`, `README.md`, `CHANGELOG.md`. If
  other tracked files are modified, list them and ask whether to include
  them (the one conditional exception).
- Commit with message: `chore: Bump version to X.Y.Z`
- Tag with: `vX.Y.Z`
- Push: `git push && git push --tags`
- Show `git log --oneline -3` and `git tag -l 'v*' --sort=-v:refname | head -5`,
  plus the changelog notes for the user to copy into the **Steam Workshop**
  description. The **GitHub** release notes need no paste: the tag-triggered
  workflow lifts this version's `CHANGELOG.md` section into the release body
  itself (and hard-fails the release if the section is missing), so the
  changelog entry written at step 6 is the release body. If step 3 updated any
  Workshop description files, list the affected languages and remind the user
  to paste each updated title and description into the Workshop page's
  per-language edit UI (Steam's own language names differ: schinese, koreana,
  brazilian, latam, ...).
