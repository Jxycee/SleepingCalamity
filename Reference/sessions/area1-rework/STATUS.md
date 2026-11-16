# Area 1 rework (Fable Run 6) STATUS

State: building. Context handoff written 2026-11-16 about 11:05 Central (session cleared and restarted by the coordinator; brief unchanged: `side-session-notes/local_88bfb1c1-.../run6-fable-area1-brief.md`).
Branch `claude/area1-rework`, worktree `.claude/worktrees/elated-dubinsky-d77725`. **Not pushed:** guard.sh wants a `gate` PASS for a push that touches Assets, and the tip is uncompiled WIP. First Unity run of the next session settles it.
Usage: start 5-hour 48%, weekly 75%, Fable 44%. At handoff (after the 11:00 reset) 5-hour 0%, weekly 82%, Fable 57%. Context was 346k.
Images used: 2 of 10 (refs + B3 sheet; baseline sheet). Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\9bc72263-ffd8-4546-b30f-19d24fe64b2b\scratchpad\run6` (a reboot clears it).

## Round and scores
Baseline (R0) shot, round 1 code half written, nothing of round 1 built or captured yet. Sheet `<scratch>\sheets\R0_baseline_sheet.jpg`, script `<scratch>\sheet.py`, JPGs in `Reference/sessions/area1-rework/baseline/`.
Strict /40, eye cameras: Spawn 14, Postcard 16, West 13, Climb 15, BellYard 9, Balcony 10, Lake 13.
Palette p10/p50/p90: Spawn .22/.53/.67, Postcard .28/.49/.66, West .33/.55/.69, Climb .23/.46/.70, BellYard .04/.25/.59, Balcony .02/.47/.74, Lake .54/.67/.78 (target .21-.25/.44-.50/.80-.86).

## Diagnosis (why it reads flat)
1. The town is a bare summit: land falls away outside the wall, the wall top sits at eye level, so every view ends in one straight horizon line. Far ridges stood only 3-5 degrees high.
2. No trees anywhere (2 m sapling cards only); ruined lots have nothing taller than a chimney.
3. Sky is a hard saturated blue HDRI with a pale flat band; sun (42, -32) front-lights the north view; shade sides go black (ambient 0.40); highlights dull (p90 .67).
4. The square is one uniform cobble plane with no cast shadows or growth.

## Look decisions (decided 2026-11-16, reversible)
- **Bell Hill:** a wooded hillside across the whole north side behind the keep. Crag bank first (6.5 m in 5 m, over 45 degrees, unclimbable), then +35 m at the terrain's north edge, about +110 m at a crest 450 m out. Town, lanes, lots, lake, stream untouched (0 south of the foot line, about z 33 behind the keep).
- **Mountain valley skyline:** mid and far ridges about doubled, Mawrrow mountain 560 -> 900 m. South stays open over the lake.
- **Real trees:** one great tree in the square at (-15, -11) in an earth pit; churchyard tree (-18.5, 28.5) and (21.8, 26.5) on the terrace; young trees in ruined lots (Ruined only, no colliders); a broken belt outside the wall; forest on the hill (3D near, cards far).
- **Light (not written yet):** town Morning sun to about Euler(33, -68) for side light on the north view and long tree shadows over the square; own sky material copy `M_HM_Sky_Morning` (rotation via `B0Fab.SkyRotationDegrees`, paler: exposure up, slightly warm tint); fog 0.0048 -> about 0.008, warmer; ambient 0.40 -> about 0.62; post exposure up about +0.25. Override inside `HollowmereLook.Apply` Morning branch only: never touch the corner's `BeautyCornerBuilder` presets or `M_BC_*`.

## Code state (uncommitted-to-origin WIP, UNCOMPILED)
- `Core/Town/HollowmereLayout.cs`: new `HillFoot(x)`, `BackHill(x,z)`; `NaturalGround` uses it in place of the old 0.14 slope.
- `Core/Town/SaltwakeRoadPlan.cs`: `FieldWeight` drops fields on the hill.
- `Editor/Builders/HollowmereTerrain.cs`: `Size.y` 44 -> 96; erosion RAW decoded with its own range (`RawOriginY/RawSizeY`) and BackHill + OuterFall added under it in `Final`; `Bake` adjusted the same way (not re-run); woodland soil in `Splat`.
- `Editor/Builders/HollowmereRoad.cs`: `RingHeight` adds BackHill; MidHills/FarRidge annuli raised; new `internal CardForest(parent, group, trees)` and `EmitTiles` (refactor of `Cards`).
- `Editor/Builders/HollowmereBackdrop.cs`: HillRidge, FarRange and the mountain raised.
- `Editor/Builders/HollowmereBuilder.cs` (committed 2be5ff09): seven `Cap_HM_Eye_*` cameras. Scene rebuilt and baseline committed in fdf60967.

## Next steps, in order
1. Write `Editor/Builders/HollowmereWild.cs` (`Reset()`, `Build(set, ruined, rebuilt, t)` called right after `HollowmereRoad.Build` in `BuildScene`; `Reset` beside `HollowmereRuinDressing.Reset`). Loads `Content/Art/Town/Trees/<code>/<code>.prefab` by path, scales to a target height from prefab bounds, falls back to the next slot when a code is missing. Ground height from `terrain.SampleHeight` on the terrain, `HollowmereRoad.RingHeight` off it. Reject spots within 2.2 m of a lane edge, 1.6 m of any path-graph edge, lots/landmarks + 2 m, the stream (7 m), water, steep (>38 degrees), any non-terrain collider (`Physics.OverlapSphere`), and within 3.5 m of another trunk. Parts: old trees (list above), town-interior trees (grid 6 m, noise gate), wall belt (PlateauDistance 35-58, clumps, keep the lake foreshore z < -62, x -45..60 clear), hill forest 3D (BackHill > 0.8, z < 95, |x| < 130, about 180 trees, conifer 45%), far forest via `HollowmereRoad.CardForest` (BackHill > 6 out to the crest, grid 9 m), boulders T11a/T11b + T12b ferns along the crag foot, square drift for Ruined (grass/weed tufts via `HollowmereFoliage.Tuft` in clumps at edges and lots, none within 1.6 m of path edges or 5.8 m of the fountain).
2. `HollowmereRuinDressing.Dress`: replace the sapling-card block with lot trees (S 1, M 1, L 2; 3.8-8 m; colliders stripped), cards as fallback.
3. `HollowmereLook`: the Morning town overrides above.
4. `Cap_HM_Eye_Balcony`: move to (-2.25, 1.65, 20.7); a parapet planter at x 0 blocks it now.
5. Trees (below), then one implementer: build + capture `SC_CAPTURE_ONLY='Eye_,HM_Overview'`, `SC_CAPTURE_SUN=Morning`, `SC_CAPTURE_PRESET=Ruined`, sheet R1 with the baseline row. Build + capture took 4.5 min; capture alone 1 min (`SC_CAPTURE_NOBUILD=1`). Then `gate`, push.
6. Rounds 2-4: judge R1 first. Expected next: standing ruin walls, keep wall surface, lake shore, ground breakup. Add a traversal check for the crag (no climb) and tree colliders; `TraversalRegressionTests` and `SaltwakeRoadPlanTests` (hedge/belt counts) are the likely failures.

## Tree subagent (stopped by the usage limit, nothing in the repo)
Brief: adopt CC0 Poly Haven trees TR01-TR07 in headless Blender, 3 LODs, textures, `AreaTreesIntake.cs` (prefab with LODGroup + trunk capsule), commit only its own paths. Done: `<scratch>\trees\candidates.md` (100 rows) and downloads in `<scratch>\trees\dl`: `tree_small_02`, `island_tree_01`, `island_tree_02`, `fir_sapling_medium`, `shrub_02/03/04`; helper scripts in `<scratch>\trees\scripts`. Not done: `sc_trees_adopt.py`, any FBX or texture, the intake C#, SOURCES rows, commit. Sources are 1.7-3.7 M triangles, so leaf cards must be culled by island, not decimated. Start a fresh implementer with the same brief plus this state (the old agent's transcript is large).

## Facts worth keeping
- Tests pin `Ground(0,30)` = 4 +-0.3 and the terraces (`HollowmereLayoutTests` 332-345); the hill starts north of z 29.5 everywhere.
- Town wall ends at z 16 on both flanks; north is open, the crag is the boundary there.
- Play camera: pivot feet + 1.5, boom 5, pitch 15 down, FOV 50: the ground fills most of the play frame, so ground breakup and cast shadows matter as much as the skyline.
- `SlopeLimit` 45 (`Core/Player/MovementMath.cs`). Keep parapet planters at x = -22.5 + 4.5k.
- Unity runs: one implementer at a time, in this worktree, `Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.HollowmereCapture.CaptureAll`.

## Run 6 lessons so far
- The scanner map and my own reads of terrain, layout, look and backdrop cost most of the 346k context before any round ran. Brief the next run with this file and skip the wide reads.
- Check `guard.sh` push rules before promising a WIP push.
