---
name: release
description: Prepare and publish a versioned release — version bumps, changelog, build, commit, tag, push
disable-model-invocation: true
argument-hint: "[major|minor|patch]"
---

# Release

Prepare and publish a new release for Archotech Android Hardware.

The user may pass a bump type as `$ARGUMENTS` (one of `major`, `minor`, or `patch`). If omitted, ask which bump type they want.

## Current state

!`git describe --tags --abbrev=0 2>/dev/null || echo "no tags found"`
!`git log "$(git describe --tags --abbrev=0 2>/dev/null || echo 'HEAD~10')..HEAD" --oneline --no-merges`

## Steps

Work through each step below **one at a time**, confirming with the user before moving to the next. Do not batch steps together.

### 1. Determine version

- Read the current version from `About/About.xml` (`<modVersion>`)
- Calculate the new version from the bump type (`$ARGUMENTS` or ask)
- Show the user: current version, bump type, and new version
- **Ask the user to confirm** before proceeding

### 2. Review changes for changelog

- Show the commit log since the last tag (already displayed above)
- If the repo has no tags yet this is the first release: use the full history
  (`git log --oneline --no-merges`) and summarise the mod's shipped feature set rather than a diff
- Draft changelog notes grouped by category (Fixes, Features, Polish/Other)
- Omit chore/version-bump commits from the changelog
- **Present the draft to the user and ask them to confirm or edit**

### 3. Refresh translation expectations and check freshness

Run, in order:
```bash
python3 Scripts/refresh-translation-expectations.py
python3 Scripts/check-translations.py --strict
```

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
  moving on — step 7 stages only the version-bump files.

### 4. Update CHANGELOG.md

- Add a new `## [X.Y.Z] - YYYY-MM-DD` section at the top of the version list,
  directly below the Keep a Changelog intro paragraph, using today's date. This
  changelog carries no `[Unreleased]` heading; don't add one
- Use the confirmed changelog notes from step 2, formatted in Keep a Changelog style (`### Added`, `### Fixed`, etc.)
- Add a `[X.Y.Z]: https://github.com/sam-hunt/ArchotechAndroidHardware/releases/tag/vX.Y.Z`
  link reference at the bottom of the file, above any older ones
- Show the diff and **ask the user to confirm**

### 5. Bump versions

Update the version string in all three files:
- `About/About.xml` — `<modVersion>`
- `Source/1.6/Properties/AssemblyInfo.cs` — `AssemblyVersion` and `AssemblyFileVersion` (four-part, `X.Y.Z.0`)
- `README.md` — version badge (`Version-X.Y.Z`)

Show the diff and **ask the user to confirm** the changes look correct.

### 6. Clean build and deploy

Run:
```bash
dotnet clean ArchotechAndroidHardware.sln
dotnet build ArchotechAndroidHardware.sln -c Release
```

Report the build result. If the build fails, stop and help the user fix it. **Ask the user to confirm** before proceeding to commit.

### 7. Stage, commit, tag

- Stage only the release files: `About/About.xml`, `Source/1.6/Properties/AssemblyInfo.cs`, `README.md`, `CHANGELOG.md`
- If there are other modified tracked files, list them and ask the user whether to include them
- Commit with message: `chore: Bump version to X.Y.Z`
- Tag with: `vX.Y.Z`
- Show `git log --oneline -3` and `git tag -l 'v*' --sort=-v:refname | head -5`
- **Ask the user to confirm** before pushing

### 8. Push

```bash
git push && git push --tags
```

Show the final result and the changelog notes for the user to copy into Steam Workshop / GitHub release notes.
