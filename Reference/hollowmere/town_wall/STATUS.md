# Q8 town wall, Lower Gate and East Gate: status

State: IN PROGRESS
Session: Q8 Town wall, Lower Gate and East Gate (cloud session, branch claude/brave-keller-3d91gk; bell tower, Q1, Q3 done before it)
Progress: 15%
Sheet: none yet
Blocker: none

Started 2026-10-31. Plan: `Art/Blender/town/sc_town_wall.py` exports a tileable wall kit (straight modules, buttress, lane-opening ends, walkway stair; Ruined
breach and crumbled variants, Rebuilt patched) and the two gatehouses (Lower Gate, East Gate; shell and restored, timber doors) on the shared B1 tileables
(Blender agent, worktree). Then the builder tiles the kit along HollowmereLayout's wall run at its heights and places the gatehouses between their piers
(Unity agent). Captures: low east, lake shore, both gates, both presets.

Running in parallel (2026-10-31):
- Art: wall kit + gatehouses generator (Sonnet implementer in its own worktree, Blender only). Contract: 4 m modules along Blender +X, outside -Y, pivot on the
  centre line at ground, footing to -1.5 m; gatehouses pivot at the passage centre, passage along Y, Shell / Frame / Part_Door / SOCKET_Door / FOOTPRINT.
- Unity: HollowmereTownWall.cs tiling code written against that contract now (deterministic module sequence along WallRun, ends at openings, buttresses,
  stairs, Ruined breach/crumbled, Rebuilt patched; gatehouses between their piers; greybox fallback while the FBX are missing), EditMode tests, Q8 cameras.
- Then: wire the real slot table from the art's SOURCES.md, capture, sheet, check, merge.
