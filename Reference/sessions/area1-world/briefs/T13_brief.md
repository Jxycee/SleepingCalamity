# T13 brief: round 2, ground and props everywhere (Area 1 world, Run 7)

Goal: the judges' lowest lines across all rounds are 8 (density of life) and 9 (cohesion): props that sit singly on bare ground, clean ground with no wear, bare earth banks, toy-like pieces beside scanned ones. T13 fixes the ground and the props across the whole area, by the user's rules, and clears the open faults list. Out of scope (later tasks): the backdrop (T22), the ruin-shell materials and the flat-shading toggle (T23), smoke/cloth/animals/audio (T24). Plan: `docs/design/area1-world-build.md` sections 1b, 3, 4 (each district's kit, density and story), 6.

Branch: `claude/area1-world`, worktree `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`, the current tip (check `git log -1`). Uncommitted `Reference/sessions/area1-world/STATUS.md` from the controller and this brief: include both unchanged in your commit, never edit STATUS.md. Do not merge main. Read the STATUS verdicts R10-R14 (judges' notes per camera) and the T12 row (decal count and headroom after its cut; keep the scene under 45-46 MB and every new mesh an asset).

## The user's rules (2026-11-18), apply everywhere in area 1
1. **Group props by use and ground each one**: props come in purposeful clusters (a cart with its load, tools by a workbench, a sack stack by a door), never a lone prop on open cobbles; every prop gets contact: a contact decal or dirt ring, debris or chips, grass or weeds at its base, slight sink or tilt. Remove props that cannot be grouped.
2. **Cobbles carry wear**: worn paths along the walking lines (darker, smoother, a little polished), dirt in the gaps, puddles in the low spots, grass and moss creeping in from the edges and along walls.
3. **Earth banks are broken up**: outcrops of the T20 scanned rocks, roots, erosion lines and gullies, fern and grass bands; no bank reads as one smooth dirt ramp (bell yard and churchyard slopes, HillWest face, keep banks, the road cuttings).
4. **No lollipop trees**: every tree reads with a trunk, branch structure and a broken crown; replace or restructure any tree that reads as a ball on a stick at eye level.

## Open faults to clear (from the verdicts and the controller's reads)
- Spawn: "props sit singly on bare cobbles", "toy-like frames beside realistic tree" (the square's wood frames and props).
- Orchard: the lane-kit garden-wall piers LK0469/LK0471 read blocky beside scanned plants: replace with T19 church-wall pieces or scanned stone.
- OrchardGap: the dark, mushy foreground.
- RoadOut: the R3_02 Mid shell's wall foot is bare dirt between clumps (ground it: rubble spill, grass, moss); leave the shell's material to T23.
- RoadBack: the scanned shrub hedge reads lumpy: vary heights, gaps, a few saplings and a fence line.
- Meadow: "flat blocky wall mismatches soft realistic grass" (the shore wall: ground it and break its top line; its material is T23's).
- HillWest: "grass tufts evenly spaced", "flat grass vs realistic trees": break the regular tuft spacing, add fern and litter (T21 plants).
- Lanes: decals on open lanes too subtle (rule 2 covers it).
- T06 cube props: the bell post, saw frame, Wren's board and washing line are scaled cubes: rebuild from plank and beam pieces (or adopted props).
- `M_RD_RoadHedge` tiles the B9 atlas 3x3 while Hedge_A/B card UVs span 0..0.6 x 0..0.5 (T10 probe): fix the UV/tiling so the Rebuilt hedges stop sampling across atlas cells.

## How
Work district by district (square and Market Street, lanes, terrace and bell yard, orchard and allotments, meadow and shore, road and hill edge). Table-driven, position-hashed rules as in the district builders. Prefer the adopted scanned props (`Content/Art/Town/Adopted/`, bounds in STATUS rows T17-T21) over primitive or low-detail pieces. Decals: use the headroom T12 leaves, few large ones before many small ones; report the count. Colliders on anything over 0.5 m the player can bump; none on small props. Megascans: name wanted listings in the report; do not download.

## Capture (at most three rounds)
Round 0: all 21 `Eye_*` cameras at HEAD with no edits into `Reference/sessions/area1-world/R15b/` (copy T12's `R15/` instead if it is from the same tip, and say so). Iterate with `SC_CAPTURE_ONLY` on the cameras of the district in hand (`SC_CAPTURE_PRESET=Ruined`, `SC_CAPTURE_SUN=Morning`). Final: all 21 from the committed code into `R16/`. Sheet: `python Reference/sessions/area1-world/tools/sheet_area.py R16 <final PNG dir> Reference/sessions/area1-world/R15b Reference/sessions/area1-world/R16`. Put each final frame beside its before and ask honestly which is better; name any view that got worse. Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\1635b7dd-88db-474e-8e1b-4f6bc1a3f6e2\scratchpad\T13`.

## Check
Build twice (identical), churn reverted (STATUS "Known churn" plus the additions in the T09-T12 rows), no meshes embedded in the scene, scene MB stated (under 46), commit by path (several commits are fine, one per district), gate (`SC MARKER` = HEAD), play loop `-Filter "HollowmereHub|LoopBot|LoopGuideHud|SquareLoopWalkQa|TraversalRegression"` (11 passed, 7 ignores), push. Delete probe scripts before committing.

## Report (under 20 lines)
Commits; gate and play loop lines; build twice; scene MB; decal count; per district what changed (clusters added, props removed, wear, banks, trees); each open fault: fixed or not and why; views that got worse; Megascans wanted.
