---
name: rimworld-logs
description: Find and read RimWorld's Player.log, or disassemble the RimWorld API to inspect vanilla types and method signatures. Use when debugging runtime behaviour, chasing an exception, or checking what a vanilla method actually does.
---

# RimWorld Logs and API Inspection

## Reading the log

1. **Enable RimWorld Dev Mode:** Settings → Dev Mode → Logging
2. **Log locations:**
   - **Windows:** `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
   - **WSL:** `/mnt/c/Users/*/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Player.log`
   - **Linux (Steam):** `~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Player.log`

Our own messages are prefixed `[Archotech Android Hardware]`, so grep for that to isolate them from
vanilla and other mods' output.

## Inspecting the RimWorld API

```bash
monodis "/mnt/c/.../RimWorldWin64_Data/Managed/Assembly-CSharp.dll"
```

The local RimWorld installation is the source of truth for API shapes — prefer it over the
`Krafs.Rimworld.Ref` NuGet package, which is only the CI fallback. `ilspycmd` works too when you
need method bodies rather than signatures.

Vanilla Races Expanded - Android (VREA) is a runtime-only dependency (no compile-time reference —
see CLAUDE.md's "Harmony Patches" section), so inspecting *its* types works the same way, against
its own assembly instead of vanilla's:

```bash
ilspycmd "$RIMWORLD_PATH/../../workshop/content/294100/2975771801/1.6/Assemblies/VREAndroids.dll" -t "VREAndroids.<ClassName>"
```
