---
name: sc-walk
description: Walk SleepingCalamity in the Unity editor's play mode through the Unity MCP with few tokens - the WalkDriver one-call entry points (fake input, press, use a target, drive the tug, state as JSON, host or join), 2-player runs (MPPM window tags or a dev build as player 2), and the context rules (small screenshots, state through JSON, hand off at ~60%). Use whenever a session plays, walks, verifies or demos the game in the editor, or runs a host + client session.
---
# Walking SleepingCalamity in the editor

Long walks belong in a subagent or child session that returns a ~5-line summary (CLAUDE.md, Agents and review). The controller orchestrates.

## Before you start
- The editor must be open on the checkout you walk (usually `C:\Dev\SleepingCalamity-check`, detached at the commit under test). Unity runs only through `Tools/unity.ps1` or the open editor.
- Read only the doc sections you need (headings first, then the lines), never whole handoff files.

## WalkDriver (`Assets/_Project/Editor/Dev/WalkDriver.cs`)
Call each through MCP `execute_code` as one line, e.g. `return SleepingCalamity.EditorTools.WalkDriver.StateJson();`. Every call returns one line or compact JSON; errors come back as `ERR <reason>`, never as exceptions.

| Call | What it does |
|---|---|
| `HostLocal()` / `JoinLocal(port)` | Turns the `SleepingCalamity.AutoSolo` EditorPref off, enters play, starts a local host (`StartLocalAsync(true)`) or joins `127.0.0.1:port`, then restores the pref. |
| `InstallFakeInput()` / `RestoreInput()` | Swaps in the tests' `FakeInputService` (reflection) via `ServiceRoots.App.Replace<IInputService>`. Real mouse input can't hold RMB through computer use. |
| `Press(edge)` | One-frame pulse: interact, equip, drop, jump, shove, journal (also reads in the Menu, so a second press closes the journal), pause, primary, secondary, primaryup, secondaryup. |
| `Hold(axis, value, seconds)` | Holds throttle, rudder/steer, reel, primary, secondary or sprint. |
| `UseTarget(name)` | Teleports next to the named `NetworkInteractable`, waits ~30 frames (the host checks its own copy of your eye), aims the camera (the interactor sphere-casts 3 m), presses E, reports the prompt before and after. |
| `DriveTo(x, z, mooring)` | TugHelm autopilot: steers by bearing, goes astern with the rudder reversed past 70°, moors when `Moorable` matches. Take the wheel first (`UseTarget("helm")`); with a mooring it drives to the mooring and ignores x, z. The mooring she just left is offered again only after she has been 12 m away (`TugMooringRules.RearmDistanceM`): back off first (`Hold("throttle", -1, 8)`), or it ends `ERR not mooring-ready`. |
| `Poll()` / `Stop()` | Multi-frame jobs (UseTarget, DriveTo) run on `EditorApplication.update`; `Poll()` returns `BUSY <step>` or the finished result. |
| `StateJson()` | Tug pose, moored/anchored, refloat/name, gear, digs, weed, lake level, boards, toasts, players. |

Query state with `StateJson()` instead of screenshots or console dumps. Toasts are buffered (last 8) only from the first `StateJson()` or `InstallFakeInput()` call. `HostLocal`/`JoinLocal` from edit mode survive the play-mode domain reload (`Poll()` says `BUSY entering play` meanwhile). Before entering play they force `EditorSceneManager.playModeStartScene` to Bootstrap (and leave a `Scenes/Dev/` scene for it), so an untitled or non-Bootstrap open scene no longer hangs Poll on "waiting for app services". `UseTarget` adds a `note` when the target's prompt is empty (a spent target, e.g. a raked `RaceWeedMass` with colliders off, is not a driver miss).

## Two players
- **MPPM window (preferred):** Project Settings > Multiplayer > Playmode tags: main editor `-dev-host-direct`, Player 2 `-dev-join-direct 127.0.0.1:7777`. Assign the tags and tick Player 2 in Window > Multiplayer > Multiplayer Play Mode, then enter play. Setting `PlayModeScenarioManager.ActiveScenario` by reflection does NOT launch a clone; use the window (computer use, if the user granted Unity).
- **Dev build as player 2 (fallback):** `$env:SC_PLAYER2_OUT='<scratchpad>\p2'; unity.ps1 exec -Method SleepingCalamity.EditorTools.WalkDriver.BuildPlayer2 -ProjectRoot ... -OutDir <scratchpad>` (editor closed; the wrapper passes no arguments, so the folder comes from `SC_PLAYER2_OUT`; `BuildPlayer2To(dir)` is the same from MCP execute_code). It builds the Build Settings scenes with `BuildOptions.Development` into `<dir>\SleepingCalamity.exe` (~3 min cold) and logs `SC BUILDPLAYER2 PASS`. Run it with its own profile or the join fails `DuplicateIdentity`: `SleepingCalamity.exe -dev-profile p2 -dev-join-direct 127.0.0.1:7777`. The editor hosts with `HostLocal()`. For a walked world copy `world.json` to `world_p2.json` first; delete the `*_p2.json` files after.
- Only one PlayMode/UDP user of port 7777 at a time (CLAUDE.md Git section).

## Context rules
- Screenshots at most 600 px wide, and few: one to confirm a look, not one per step.
- State through `StateJson()`; console through `read_console` filtered to errors/warnings with a small count.
- Hand off to a fresh session at ~60% context (commit, write a handoff note, start the next session).
