# Area 1 world (Fable Run 7) STATUS

State: **building** (started 2026-11-16 17:30 Central). Brief: `side-session-notes/local_88bfb1c1-.../run7-fable-whole-area1-brief.md`. Plan: `docs/design/area1-world-build.md` (districts D1-D11, cameras, rules, tasks T01-T14).
Branch `claude/area1-world` (from `origin/claude/area1-rework` 44a12aa2), worktree `.claude/worktrees/elated-dubinsky-d77725`. Push with `git push -u origin claude/area1-world` (the branch was created tracking area1-rework).
Scratch (a reboot clears it): `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\0975e27d-5568-4784-b3db-e3319868a89d\scratchpad\run7`.
Usage at start: 5-hour 45% (resets 21:00 Central), weekly 64%, Fable 9%. Stops: weekly 92%, 5-hour 90%.
Images used by the controller: 1 of 16 (Run 6 R4 sheet).
Code Mode: not offered in a Desktop session.

## Task progress
| Task | State | Commit | Notes |
|---|---|---|---|
| Plan (Phase 1) | done | this commit | |
| T01 cameras, sheet tool, baseline, scene composition | running | | Unity |
| T02 ruin shell kit (Blender) | running | | stages to `Art/Staging/area1-world/shells/` |
| T03-T14 | not started | | see the plan, section 6 |

## District scores (strict /40, weakest camera; Ruined, Morning)
Baseline not shot yet. Run 6 end: Spawn 22, Postcard 25, West 22, Climb 20, BellYard 17, Balcony 22, Lake 15.

## Decisions (2026-11-16, current direction)
- The Ruined preset in Morning light is the judged state; Rebuilt gets one sanity sheet at the end.
- Ruin shells are whole replacement ruin meshes in three height classes per lot (low = today's kit), picked by a per-lot table.
- Orchard trees are TR02/TR03 with a yellow-green leaf variant; no new apple tree model this run.
- Ground cover goes into terrain detail layers with district masks, not scene objects.
- APV is a late bake trial (T12), not a foundation.
- No Abbey frames exist in `Reference/private/`; the reference row is our concepts H1-H8.

## Facts found by the scans (2026-11-16)
- Scene 59.5 MB: 9,441 GameObjects, 7,879 renderers, 2,446 prefab instances, 479 embedded meshes.
- In Ruined every lot is `Empty` and gets `Ruins/Ruin_<S|M|L>_<A|B|C>` through `HollowmereBuilder.PlaceRuin` (:638-660); `HollowmereRuinDressing.Dress` adds ivy, debris, saplings, a crow.
- No decal projectors, no APV, no local Volumes, no GPU instancing outside terrain detail (`HollowmereFoliage.TerrainGrass`). The volumetric fog package (`com.cqf.urpvolumetricfog`) is installed.
- Adopted but never placed: T13 boat. No orchard builder, no road boundary, no churchyard.
- Camera table: `HollowmereBuilder.cs:39-107`; capture env vars in `HollowmereCapture.cs` (see Run 6 STATUS "How to run a round").

## Next
T01 and T02 report; then T03 (scene diet) beside T04 (nature set); then T05 (shells into the town) and the first sheet.
