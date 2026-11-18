# T13 brief (run T13a): round 2, ground and props everywhere (Area 1 world, Run 7)

Goal: the judges' lowest lines across all rounds are 8 (density of life) and 9 (cohesion): props that sit singly on bare ground, clean ground with no wear, bare earth banks, toy-like pieces beside scanned ones. T13 fixes the ground and the props across the whole area, by the user's rules, and clears the open faults list. Out of scope (later tasks): the backdrop (T22), the ruin-shell materials and the flat-shading toggle (T23), smoke/cloth/animals/audio (T24). Plan: `docs/design/area1-world-build.md` sections 1b, 3, 4 (each district's kit, density and story), 6.

Branch: `claude/area1-world`, worktree `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`, the current tip (check `git log -1`). Uncommitted `Reference/sessions/area1-world/STATUS.md` from the controller and this brief: include both unchanged in your commit, never edit STATUS.md. Do not merge main. Read the STATUS verdicts R10-R14 (judges' notes per camera) and the T12 row (decal count and headroom after its cut; keep the scene under 45-46 MB and every new mesh an asset).

## The user's rules (2026-11-18), apply everywhere in area 1
1. **Group props by use and ground each one**: props come in purposeful clusters (a cart with its load, tools by a workbench, a sack stack by a door), never a lone prop on open cobbles; every prop gets contact: a contact decal or dirt ring, debris or chips, grass or weeds at its base, slight sink or tilt. Remove props that cannot be grouped.
2. **Cobbles carry wear**: worn paths along the walking lines (darker, smoother, a little polished), dirt in the gaps, puddles in the low spots, grass and moss creeping in from the edges and along walls.
3. **Earth banks are broken up**: outcrops of the T20 scanned rocks, roots, erosion lines and gullies, fern and grass bands; no bank reads as one smooth dirt ramp (bell yard and churchyard slopes, HillWest face, keep banks, the road cuttings).
4. **No lollipop trees**: every tree reads with a trunk, branch structure and a broken crown; replace or restructure any tree that reads as a ball on a stick at eye level.

## Split (coordinator 15, 2026-11-18)
T13 runs as two implementer runs, one after the other, each with its own three-round capture cap. **This run is T13a**: districts square and Market Street, the lanes (West, Ring West, Ring South, Lower Lane, Inn row), the terrace, the bell yard and the churchyard, plus the area-wide material fixes below. T13b (orchard and allotments, meadow and shore, mill, road and hill edge, HillWest, RoadOut, RoadBack) gets its own brief after T13a lands; do not touch those districts except through an area-wide material fix.
T13a cameras (exact names): `Eye_Spawn`, `Eye_Postcard`, `Eye_West`, `Eye_Bakery`, `Eye_InnFront`, `Eye_MarketStreet`, `Eye_LowerLane`, `Eye_RingWest`, `Eye_RingSouth`, `Eye_Climb`, `Eye_BellYard`, `Eye_Balcony`, `Eye_Churchyard` (13). There are 23 `Eye_*` cameras in all.

## Open faults to clear in T13a
- Spawn and Postcard: "props sit singly on bare cobbles", "toy-like frames beside realistic tree": the square's wood frames and the loose props lying on open cobbles (sacks, gourds, pots in Postcard's foreground). Group them by use against walls, doors and stalls, ground each one, remove what cannot be grouped.
- RingSouth: T12's decal thinning cleaned the lower-right lane (R14b had a dirt and moss patch on the cobbles at the lane edge, R15 lost it). Restore that wear, through the route below, not by un-thinning.
- Lanes: decals on open lanes too subtle (rule 2 covers it).
- Bell yard and churchyard: bare earth slopes behind them (rule 3).
- T06 cube props: the bell post, saw frame, Wren's board and washing line are scaled cubes: rebuild from plank and beam pieces (or adopted props).
- `M_RD_RoadHedge` tiles the B9 atlas 3x3 while Hedge_A/B card UVs span 0..0.6 x 0..0.5 (T10 probe): fix the UV/tiling so the Rebuilt hedges stop sampling across atlas cells (check with one Rebuilt capture of `Eye_RoadBack`).

## Budgets and pipeline (hard)
- **Decals: at most 340 area-wide** (304 now). Decals are for contact under prop clusters only. **Cobble wear, dirt in the gaps, puddles and grass creep come from the ground itself**: the cobble/street material's wear mask, vertex colour or height blend, terrain layer paint, and mesh debris and plants. Find how the square and lane surfaces are drawn first and say which route you used. Report the final decal count.
- New contact-decal name prefixes go on `HollowmereDecalThin`'s never-drop list (it reads names only), or its thinning deletes your grounding.
- New builder calls go inside the builder list in `HollowmereBuilder` (before `HollowmereCombine.Apply` / `HollowmereLods.Apply`, which run over the whole scene), so new small props are combined and Mesh-LOD'd. Any newly used adopted FBX gets the same importer settings T12 gave the others (Mesh LOD levels in `HollowmereLods`, Read/Write where Combine needs it).
- Scene under 45.5 MB (44.24 now), no embedded meshes.

## How
Work district by district (square and Market Street, lanes, terrace with bell yard and churchyard). Table-driven, position-hashed rules as in the district builders. Prefer the adopted scanned props (`Content/Art/Town/Adopted/`, bounds in STATUS rows T17-T21) over primitive or low-detail pieces. Decals: use the headroom T12 leaves, few large ones before many small ones; report the count. Colliders on anything over 0.5 m the player can bump; none on small props. Megascans: name wanted listings in the report; do not download.

## Capture (at most three rounds)
Round 0 is `Reference/sessions/area1-world/R15/` (only `Reference/` changed since e19a7520; no new capture). Iterate with `SC_CAPTURE_ONLY` on the cameras of the district in hand (`SC_CAPTURE_PRESET=Ruined`, `SC_CAPTURE_SUN=Morning`). Final: the 13 T13a cameras from the committed code into `R16a/`. Sheet: `python Reference/sessions/area1-world/tools/sheet_area.py R16a <final PNG dir> Reference/sessions/area1-world/R15 Reference/sessions/area1-world/R16a`. Put each final frame beside its before and ask honestly which is better; name any view that got worse. Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\54c4d5da-7980-453e-9883-d2236b06f4b2\scratchpad\T13`.

## Check
Build twice (identical), churn reverted (STATUS "Known churn" plus the additions in the T09-T12 rows), no meshes embedded in the scene, scene MB stated (under 45.5), commit by path (several commits are fine, one per district), gate (`SC MARKER` = HEAD), play loop `-Filter "HollowmereHub|LoopBot|LoopGuideHud|SquareLoopWalkQa|TraversalRegression"` (11 passed, 7 ignores), push. Delete probe scripts before committing.

## Report (under 20 lines)
Commits; gate and play loop lines; build twice; scene MB; decal count; per district what changed (clusters added, props removed, wear, banks, trees); each open fault: fixed or not and why; views that got worse; Megascans wanted.
