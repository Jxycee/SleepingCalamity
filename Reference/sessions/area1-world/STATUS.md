# Area 1 world (Fable Run 7) STATUS

State: **T07 running (2026-11-17 late, resumed after a /clear on Opus 5.5 high).** Judging from now on: scene-judging v2 (`origin/main:docs/art/scene-judging.md`): /50 card, two blind `reviewer` runs with the anchors (`Reference/private/judging-v2/anchors/`), A/B with diff gate and null pairs; old /40 numbers below are not comparable. Megascans: the whole library is available through the Epic Launcher (Mid, 2K); prop prep to Haiku batches. Work silently (user rule): text only for work, a decision, a blocker, READY FOR CLEAR or run done. A restarted session needs only this file and the plan `docs/design/area1-world-build.md`. Brief: `side-session-notes/local_88bfb1c1-.../run7-fable-whole-area1-brief.md`.
Branch `claude/area1-world` (from `origin/claude/area1-rework` 44a12aa2), worktree `.claude/worktrees/elated-dubinsky-d77725`, upstream set (`git push` works).
Scratch (a reboot clears it): `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\6cea6def-8e61-49b6-8ea3-1619f7abe648\scratchpad\run7` (rounds up to R8: `...\0975e27d-5568-4784-b3db-e3319868a89d\scratchpad\run7`).
Code Mode: not offered in a Desktop session.

## Usage
- Start (17:30): 5-hour 45%, weekly 64%, Fable 9%.
- The 5-hour limit hit at about 19:10 and cut T03 and T04 mid-run. After the reset (21:02): 5-hour 14%, weekly 74%, Fable 20%.
- So the first window cost 10 weekly points for: three Haiku scans (310k tokens), the plan, T01 (156k), T02 in two rounds (280k), T04 (finished and committed before the cut), a few minutes of T03.
- **User direction relayed by the coordinator (2026-11-16, about 21:00):** no image budget; no weekly-usage stop; the full task list stands (road boundary, LOD pass, APV trial included); implementers may remove, add, edit or replace anything in area 1 for the best result (the playable loop and its tests still hold); study the Abbey's images in the browser. Context handoff moves to about 450k: write STATUS and tell the coordinator.
- **Run one implementer at a time** (parallel runs filled the 5-hour window in 100 minutes). The 5-hour stop at about 90% still holds.
- Abbey images studied in the browser pane (nine shots); the lessons are in the plan, section 1b.

## Task progress
| Task | State | Commit | Notes |
|---|---|---|---|
| Plan (Phase 1) | done | 74fa409f | 11 districts, 20 eye cameras, rules, tasks |
| T01 cameras, sheet tool, baseline, scene composition | done, gate PASS 1859/1859, pushed | 4dbeb2f3 | `R0/` JPGs and sheets A/B; `tools/sheet_area.py`, `tools/scene_composition.py`; `scene-composition.md` |
| T02 ruin shell kit | done (2 rounds), not yet in Unity | 34568e7b, 2ce1fa03 | `Art/Staging/area1-world/shells/` (23 FBX), generator `Art/Blender/town/sc_ruin_shells.py` |
| T04 nature set | done by its commit; its final report was cut by the limit, so unreviewed | 75329ede | `Art/Staging/area1-world/nature/N01..N07` and `SOURCES.md`; contact sheet in scratch `nature\contact_sheet.png` (not looked at) |
| T03 scene diet | done, pushed; gate PASS 1859/1859 but run before the commit (marker is not e1e9ca72: T05's gate covers it) | e1e9ca72 | scene 59.6 -> 38.6 MB; 274 LFS mesh assets in `Content/Generated/Hollowmere/` through one scene-level pass (`GeneratedMeshes.Externalize`); build twice identical; three-camera blurred difference under 0.6/255 |
| T05 shells into the town | done, pushed; gate PASS 1859/1859, marker = fa5a9b77; play loop PASS (11 passed, 7 existing ignores) | 78a035d2, 90d3812a, fa5a9b77 | 29 shells (Mid 14, Tall 15), 503 shell BoxColliders, 160 rubble pieces, 104 wall-foot tufts; cameras HillWest moved, Churchyard added, Mill moved; High cameras fog off; `R1/` sheets |
| T08a ground outside the wall | done, pushed; gate PASS 1859/1859, marker = 6222f837; play loop not run (no collider change) | 6222f837 | probe: bank = steep-rule cobble, flat meadow = the grass layer itself bare under sun (no mesh over it); fine-blade carpet (3 meshes x 3 tints, 8.5/m2), tall clumps as accents only, N06/N07 flower drifts, bare-earth and dry patches, earth bank outside the wall; N01-N07 adopted into `Content/Art/Town/Adopted/N0x_*` with materials, prefabs, LODGroups (`NatureIntake.cs`); `R2/` sheets and `meadow_crop.jpg`; inside-wall views unchanged (blurred diff under 0.53/255); 334,602 detail instances, worst eye camera about 2.9M grass triangles |
| T08b shore, bank, mill | done, pushed; gate PASS 1859/1859, marker = c3ca712a; play loop PASS (11, 7 ignores) | c3ca712a | new `HollowmereShore.cs`; reed beds (3 x 22), shore band, 123 stones, 2 boats (T13, boxes), logs, stumps; bank 18 N02 + 3 N01 (boxes), rubble, 108 shrubs, 144 ferns; 28 tree bases; mill yard; road hedges = Hedge_A/B with leaf tiled 3x3 (`M_RD_RoadHedge`; card hedges read as saplings); inside diff 0.44/255; build twice identical; `R3/` sheets |
| T08c bank | done, pushed; gate PASS 1859/1859, marker = 19514f61; play loop PASS (11, 7 ignores) | 19514f61 | new `HollowmereRevetment.cs`: generated fieldstone face on the steep outer bank (22 meshes of 24 m, about 25k tris per preset, externalized `RevRuined_*`/`RevRebuilt_*`), slumps, ivy, foot plants, ends at lanes with a pier; inside diff 0.44/255; build twice identical; `R4/` sheets. Round 1 (5th terrain layer) failed and was removed | the slope is 45-72 deg: terrain layers stretch and the sun lights it flat; the 5th layer ignores `diffuseRemapMax` and turns height blend off for the whole terrain (dropped); kept: shore band no longer paints the bank as Dirt, `BuilderUtil.NormalizeSceneFile` IOException retry |
| Shell round 3 | done, pushed; gate PASS marker = 00e73432; play loop PASS | 313a1fd3, 620aceab, 00e73432 | section "Shell round 3" |
| T06 streets | done (partial read), pushed; gate PASS 1859/1859, marker = f812d37e; play loop PASS (11, 7 ignores) | f812d37e | `HollowmereStreets.cs` (300 decals; DecalRendererFeature ScreenSpace on `BeautyCorner_Renderer` and `PC_Renderer`), `HollowmereStreetClutter.cs` (81 clusters / 206 props, 3 landmarks, 11 story props, 245 tufts, 70 spill pieces); `HollowmereClutter.cs` is the Q14 builder (only `Instantiate` made internal); build twice identical; scene 46.29 MB (over the 45 MB target, under the 50 hard limit: T12); `R8/` sheets |
| T17 Megascans intake for T07 | done (Haiku), pushed | 70fb81b0, 323ea53d (gate PASS 1859, marker 323ea53d) | T17a/T17b crates, T17c bucket via `B1TownIntake` (A04 skip fixed to a prefix); T17d hay-pile decal staged. Launcher: Fab download location is now `C:/Users/jayce/AppData/Local/com.epicgames.eglv2/FabLibrary/` (not VaultCache), preferred format FBX, quality tier unset (pick Mid in the dialog; 3D listings downloaded Mid 2K). Items not yet seeded in the user's library: add them on fab.com in the browser pane (user signed in), then download in the Launcher (user, 2026-11-17). |
| T18 Megascans for T09/T10 | done (Haiku), pushed; gate PASS 1859, marker = 2591e5b0 | 2591e5b0 | `Adopted/T18a-c` Old Gravestones (0.6-0.95 m wide, 1.1-1.37 m tall), `T18d` Large Fallen Tree (2.0 x 0.8 x 5.9 m, road boundary), `T18e` Mossy Wooden Log (1.46 m). Fab: user has ~8.8k of ~22k Megascans in the library (seeding); Launcher supports ctrl+click multi-select download; export target stays unset (raw files only, our intake does the rest); headless tools (FabCLI, fab-downloader) researched and not used (`scratchpad/run7/fabcli/findings.md`). |
| T07 orchard and allotments | done, pushed; gate PASS 1859/1859, marker = 848ed2ae; play loop PASS (11, 7 ignores); build twice identical | 848ed2ae | new `HollowmereOrchard.cs` (7 apple crowns TR02/TR03 yellow-green, fallen apple tree with pit, 20 windfalls in 5 clusters, harvest props, 2 nooks, worn path, 88 decals incl. new `M_ST_Decal_Hay`, ~198 tufts, 1 fill light); Ruined allotments on R2_07/R2_18/R2_22/R3_02 (`HollowmereGarden` Loose: T16f pen fences, bolted beds, one replanted bed on R2_18, trellises, water butt + bucket); wall gap = visual Crumbled module, straight module's collider kept (4.9 m vs jump 1.2 m); camera `Cap_HM_Eye_OrchardGap` (40.2, 1.65, -53.2) -> (46.5, -64); scene 47.94 MB; area decals ~388 (over the 300 budget: T12); `R9/` |
| T07b orchard fix round | next (after the T18 intake) | | brief in scratch `run7/T07b_brief.md` |
| T09 onward | not started | | order below |

## Verification rules in force (coordinator's note `verification-practice.md`)
Each report is checked against `git show --stat` and the sheet; before and after from the same cameras; no stray `.unity`/`.prefab`/`.mat` in a commit; gate after the commit so `SC MARKER` equals HEAD; play loop tests for any collider or ground change; judged against the concept row; three failed rounds on one item means stop and ask.

## Known churn (not ours; revert before every commit, never commit)
Full list for briefs (2026-11-17): M_GD_Leaf, M_GD_Produce, M_GD_Straw, M_GD_patch_rust, M_GD_patch_seam, M_StoneCottage_B_SC_Rubble, M_HM_WallIvy, M_HH_OldStone, M_HM_GrassAccent, M_HM_GrassFine_Dark/Dry/Mid, M_RD_RoadHedge (.mat), M_Slime_Glass.mat, T_Water_N.png.meta, Prefabs/CellarFloorSync.prefab, Scenes/CellarFloorTest.unity. Generator exports in `Art/Staging/area1-world/shells/` are untracked duplicates of the committed Content FBX: delete them. Original note: a plain `build` rewrites, the same way every time: `M_GD_Leaf.mat`, `M_GD_Produce.mat`, `M_GD_Straw.mat`, `M_GD_patch_rust.mat`, `M_GD_patch_seam.mat`, `M_StoneCottage_B_SC_Rubble.mat` (float noise in `_Color`), `Assets/_Project/Prefabs/CellarFloorSync.prefab`, `Assets/_Project/Scenes/CellarFloorTest.unity`; once also `Content/Materials/Enemies/M_Slime_Glass.mat`. A churn bug for the coordinator.

## Rejection list (tried, did not work; do not repeat)
- Stair treads sharing one mesh by dimensions: each tread is seeded from its step id (joints, jitter, UV), so sharing would change the stairs' look. Left as 220 assets.
- HillWest camera at (-36, 23) or (-38, 23) looking east: it faces the keep's 7 m west retaining wall in shade. Moved in T05 to look north-north-east at the crag foot; the churchyard gets its own camera on the terrace.
- All-stone ruin shells with single-block stacks (shell kit round 1): read as generic stone ruins, not Hollowmere houses. Round 2 has stone below, charred half-timber and plaster above.

## R2 verdict (after T08a; strict /40, judged on `R2/R2_sheet_B.jpg` against R1)
Changed views: Meadow 8 -> 15, Lake 13 -> 18, JettyBack 12 -> 17, Mill 15 -> 20, RoadOut 17 -> 19, RoadBack 15 -> 16. The rest as R1 (inside the wall unchanged by design). Mean of the 21 eye cameras about 17.
- The grass is now a real carpet with colour variety and flowers; the meadow and the lake shore read as meadow.
- **The bank under the town wall is still the worst surface**: a smooth, pale grey-green slope (no cobble now, but no rock, shrub or texture break), wall foot to meadow in one plane (Meadow, JettyBack). Needs rocks, shrubs, darker earth, a ragged foot line.
- **The road hedges in RoadBack still read as giant cabbages**: T08a shrank the field-hedge cards, but the hedges in that view are the `Hedge_A/B` FBX props (`RoadItemKind.Hedge`), which were not touched. Replace or rescale those.
- The lake shore is one clean line of grass to water: no reeds, stones or earth band; the far shore is a thin strip.
- Orchard (10) untouched: it is T07's.

## R3 verdict (after T08b; strict /40, judged on `R3/R3_sheet_B.jpg` against R2)
Meadow 15 -> 16, Lake 18 -> 20, JettyBack 17 -> 17, Mill 20 -> 20, RoadOut 19 -> 19, RoadBack 16 -> 20. Inside the wall unchanged (diff 0.44/255).
- Road hedges now read as hedges (leaf tiled 3x3 on the FBX). Shore has an earth/shingle band, a beached boat and reeds (Lake).
- **The bank under the wall is still a smooth pale tan plane** (Meadow, JettyBack): the stones and tufts on it are too few and too small to break it. Cause (read in `HollowmereTerrain.cs` :202-261): the slope e 29.5-36 is outside (`ow` = 1) and paints the `Dirt` layer (`packed_soil`, pale) at 0.3-0.8 weight; the agent could not darken it because that layer also covers the lots inside the wall. T08b spent about four blind rounds on it.
- T08c fix (cause-based, one round; if it fails, ask the user): a fifth terrain layer `BankEarth` (a darker, rootier forest soil, existing texture or Poly Haven CC0 via `sc_fetch_*`, tinted with `diffuseRemapMax` if needed) painted only where `ow` = 1 in place of `Dirt`; on the bank the grass layer stays dominant with the dark earth in ragged 1-3 m patches; the fine-blade carpet continues up the bank at reduced density; more and bigger half-sunk N02/N01 on the bank (every 6-10 m) and a ragged foot line.
- **Decided (user, 2026-11-17): stone revetment.** The town wall continues down the steep outer bank as a battered masonry base with moss, ivy, rubble and grass at its foot (generator-built, no Core change). Rejected: terrain-layer paint on the 45-72 deg face (7 rounds), a 5th terrain layer, mesh earth skins, cobble flecks.
- New churn: `M_HM_GrassAccent`, `M_HM_GrassFine_Dark`, `M_HM_GrassFine_Dry`, `M_HM_GrassFine_Mid` flip `_Color` between builds (revert, never commit). IOException 1224 on `Hollowmere.unity` save about every second run; a retry passes.

## R4 verdict (after T08c; strict /40, `R4/` Meadow and JettyBack against R3)
Meadow 16 -> 20, JettyBack 17 -> 21; the rest unchanged. The bank now reads as a stone revetment that belongs to the wall, with a slump of earth in JettyBack.
- For T13 round 2: the face is too clean and regular up close (Meadow): running bond reads tiled, the moss and damp masks barely show, the foot meets the ditch earth in a hard line at the bottom left. Wanted: visible moss and damp on the lower half, more size variation and a few missing or pushed-out stones, grass tufts in the joints.
- New churn: `M_RD_RoadHedge.mat` flipped once; `T_Water_N.png.meta` once.

## Shell round 3 (313a1fd3; gate PASS 1859/1859 marker = 313a1fd3; play loop PASS; build twice identical)
Blender: courses 0.25-0.35 m with quoins, stacks 1.6-1.8 m breast tapering to a 0.9-1.1 m shaft with cap, pots, soot; thick charred gable frames, plaster grid with lath; moss/char wear; 3 cm stone overlap and a dark FK_PlinthJoint backer per course. Unity: Tall lots turn the door to the lane (`FacingYaw`) and take the B or C file whose gable faces the square (Core untouched). R5 verdict (9 square cameras against R4): walls read as real masonry now (Bakery 18 -> 19), but three defects:
- The Bakery stack shows a checker of dark holes (missing stones over the dark backer).
- No wear shows in Unity: no moss at wall feet, no char at tops, no soot band on stacks; all shells read one clean sandstone.
- No gable timber is visible from any of the nine eye cameras.
Round 3b (620aceab; gate PASS marker = 620aceab; play loop PASS; build twice identical): wear now shows (own `M_RuinShell_*` materials, `_SC_WEARGB` keyword in `SC_WearLit` with strong constants: moss feet, char tops, soot band, charred brown oak, limewash plaster); stack jitter flattened. R6 verdict: Bakery 19 -> 20 (moss at wall feet reads); the rest as R5.
- **The Bakery-view right stack still shows the dark-square checker**, unchanged from R4/R5: the 3b fix (stack jitter in the generator) did not touch it, so that stack is probably not a shell (low kit `sc_ruin_kit.py` stack, or another builder). Next round: identify the object by a probe (renderer under that pixel) before any fix.
- **Gables are edge-on from every lane** (door to the lane, gable on a side wall); only RingWest shows one, small. Concept H4 is a gable-fronted house. Next round: Tall shells put the charred gable over the door end (front gable, door in the gable wall, stack to the back or side), so the gable faces the lane.
- Moss at wall feet is a vivid green in sun: mute it toward olive-grey.
Round 3c (00e73432; gate PASS marker = 00e73432; play loop PASS; build twice identical for the scene and FBX metas): the checker was F02's Mid shell (`Ruin_L_B_Mid`), cause smoothed normals (every 0.3 m stone shaded as a pillow under the grazing sun); shells now flat-shaded. Tall shells have a front gable over the door wall (A stack back-centre, B back-left, C left side wall), `VariantFor` returns Core's variant again; moss olive-grey. R7 verdict (`R7/R7_sheet_A.jpg`): Spawn 22 -> 23, West 22 -> 23, Bakery 20 -> 22, InnFront 18 -> 19, Balcony 22 -> 23, Postcard 25. Gable frames now show in Spawn, West, Balcony, RingWest, small; fuller frames (fewer open bays) are a T13 item. Shell round 3 closed.

## R9 verdict (after T07; scene-judging v2 A/B, one Sonnet reviewer, null pair Spawn R8/R8 = tie, so trusted)
- Orchard R8 vs R9 (diff 28.8): **R8 preferred**, medium: "R8 shows readable trunks, lit lawn, wall and sky; R9 is a dark foliage mass hiding the wall"; line 4 3 vs 3, line 10 3.5 (R8) vs 2.5 (R9). None of the story props is in either orchard camera.
- RingSouth R8 vs R9 (diff 0.83): tie. InnFront, Mill, Meadow under the 0.5-0.62 diff gate (no change).
- My read of OrchardGap (new, no before): Wall_Breach rubble reads as big clean floating boxes on a bare grey-green plane; windmill visible through the gap, lake not.
- T07 left: scarecrow not placed (spot failed placement), only 3 solid props, allotment plots seen only from removed diagnostic cameras. All in the T07b brief.

## R8 verdict (after T06; strict /40, `R8/R8_sheet_A.jpg` and `_B.jpg` against R7)
Spawn 23 -> 24 (grime and damp patches on the square), Bakery 22 -> 24 (Tobin's bench, sacks, crates at the step), InnFront 19 -> 20 (bench and table), MarketStreet 17 -> 18 (handcart small in frame); the rest unchanged. Working mean of the 21 eye cameras about 20.
For T13 round 2 (T06 leftovers): decals on open lanes too subtle (RingSouth cobbles still clean); clutter under target in D4/D5/D6 (294 candidates rejected by placement rules: loosen against walls); bell post, saw frame, Wren's board and washing line are scaled cubes (rebuild from plank/beam pieces); no decals on the Market Street stair; InnFront table half hidden behind the gate pier.
Check first thing next round: a bright red slab on the BellYard path (R8 `HM_Eye_BellYard_Ruined.jpg`, mid-left of the lane): find what it is (T06 prop, a missing material, or older), fix if it is a bug (fold into the T07 or T09 brief).

## Resume order (2026-11-17 evening, after the second handoff)
1. Done: T08b (c3ca712a), T08c (19514f61, stone revetment), shell round 3 (313a1fd3, 620aceab, 00e73432), T06 (f812d37e).
2. Next: **T07** orchard and allotments (plan section 4 D7 and its T07 row); **T09** bell yard, churchyard, crag (N01-N03; **first: find and fix the bright red slab on the BellYard path**, R8 `HM_Eye_BellYard_Ruined.jpg` mid-left); **T10** road out, boundary, hill edge; **T11** atmosphere and backdrop (mountain spike, flat far ranges); **T12** LOD and APV trial (scene size 46.3 MB to bring under 45); **T13** round 2 (leftovers listed in the R4, R7 and R8 verdicts; **before any T13 work on shells: a same-camera toggle of round 3c's flat shading vs smoothed on Postcard**, because scene-judging v2 section 4 found Postcard R6 judged better than R7 in 3 of 4 blind looks, "cleaner, less ruined walls"); **T14** landing.
How the last rounds were run (worked well): one Sonnet implementer at a time with a full brief (files, do-list, capture command with `SC_CAPTURE_ONLY` / `SC_CAPTURE_PRESET=Ruined` / `SC_CAPTURE_SUN=Morning`, at most three capture rounds, sheet with `sheet_area.py R<n>`, build twice, churn list, commit then gate then play loop `-Filter "HollowmereHub|LoopBot|LoopGuideHud|SquareLoopWalkQa"`); controller edits STATUS uncommitted and the implementer includes it in its commit; judge from `git show --stat` and the sheet; a fresh implementer per fix round (resuming a 300k agent re-reads its transcript). Diagnose before fixing: the bank (terrain angle) and the stack checker (smoothed normals) were both found only by a probe after blind rounds failed.

## T08b brief (shore, wall bank, mill; Unity)
Goal: the lake edge and the slope under the town wall read as natural ground; the mill sits in a dressed yard. Owns `HollowmereLakeArt.cs`, `HollowmereTerrain.cs` (outside the wall only), `HollowmereRoad.cs` (hedges), a new `HollowmereShore.cs` if wanted. Use the adopted N01-N05 rocks, stump, log (`Content/Art/Town/Adopted/N0x_*`, prefabs with LODGroups), T11a/T11b, T13 boat, T12 ferns.
- Wall bank (all around the outside of the town wall, e 28-34 from the plateau): darker, richer earth under the grass; 1-2 m rubble and fallen wall blocks at the wall foot (T05b/c, N03), N02 boulders and N01 outcrops half sunk every 8-15 m on the steeper parts, shrubs (TR05-TR07) and ferns in clumps, a ragged foot line between the bank and the meadow. Never in front of the Lake or JettyBack cameras within 5 m (`HollowmereBuilder.cs` camera table; the Lake camera stands on the road out of the Lower Gate at (-21.5, -73)).
- Shore: an earth and stone band 0.5-2 m wide at the waterline (soil splat plus N03 and T11b stones), three dense reed beds (existing reeds in `HollowmereLakeArt.ShoreReeds`, about :236, grouped instead of evenly scattered), one boat drawn up on the shingle near the jetty and one moored (T13), a few logs (N05) and a stump (N04) near the water, short-range shoreline foam only if `SC_Water.shader` already supports it.
- Mill (69.5, -58.5): sacks, a cart (T16b), a fence run (Fence_Rail), firewood (T06), a millstone leaning on the wall; the Mill camera's foreground is grass, keep it so.
- Road hedges: replace the `Hedge_A/B` props on the Saltwake Road with smaller-leaved hedges (rebuild from cards like the field hedges T08a made, or scale the leaf texture down by 3) so a hedge reads as a hedge at 2 m.
- Tree bases in walking range outside the wall (plan 1b point 7): an earth mound, 2-3 stones, a grass ring under each 3D tree within 20 m of a lane or the shore.
- Verify: R3 capture of all cameras against R2 (`sheet_area.py R3 <S>\r3 .../R2 .../R3`), inside-wall blurred diff for Spawn, Postcard, West under 1/255, determinism (build twice), churn reverted, gate after the commit with marker = HEAD, play loop tests (rocks and boats add colliders), traversal regression without a baseline change (no rock on the road, the shore path or the jetty), push.

## Shell round 3 brief (Blender then Unity)
Why: in every square view the shells read as stacked blocks, not burnt houses (R1 verdict). Owns `Art/Blender/town/sc_ruin_shells.py`, the 18 shell FBX in `Content/Art/Town/Ruins/Ruin_*/`, `HollowmereRuinShells.cs`.
- Masonry scale: courses of 0.25-0.35 m stones (today about 0.5 x 0.3 m faces that read as slabs at 10 m); change the block size in the generator (the base kit's `soft_box` courses), not only the UV.
- Stacks: they read as thin pillars at 20 m. Wider breast at the bottom (1.6-1.8 m), clear taper, a soot-dark band on the top 1.5 m, pots on half of them.
- Gables: the charred half-timber and plaster gable must face the lane or the square, not the back: in `HollowmereRuinShells.cs` pick the variant or a yaw per lot so the gable end faces the nearest lane (keep the doorstep on the lane side; if both cannot hold, the doorstep wins).
- Wear: strong low-frequency moss and damp stain on the lower walls and north faces (`wear` G), char at the broken tops (`wear` R); rubble spill already exists.
- Keep the contract (nodes, `FOOTPRINT_COL_<n>` boxes, slot names, budgets S 5k / M 8k / L 12k).
- Verify: Blender contact sheet once, then R-capture of Spawn, Postcard, West, Bakery, InnFront, Balcony, RingWest, RingSouth, MarketStreet against the previous round, traversal regression unchanged, gate after the commit, play loop tests, push.

## R1 verdict (after T05; strict /40, judged on `R1/R1_sheet_A.jpg` and `_B.jpg` against the concept row)
Spawn 22, Postcard 25, West 22, Bakery 18, InnFront 18, Climb 20, Balcony 22, BellYard 17, Churchyard 17, HillWest 14, MarketStreet 17, LowerLane 16, RingWest 18, RingSouth 14, Orchard 10, Mill 15, Meadow 8, Lake 13, JettyBack 12, RoadOut 17, RoadBack 15. Weakest district: D9 meadow and shore (8).
What T05 did and did not do:
- Stacks now stand above the square's skyline (Spawn, West, Postcard, Balcony) and RingWest has a real tall ruin with ivy. The town reads as ruins from the high views.
- **The shells read as stacked blocks, not burnt houses.** Masonry blocks are far too big (about 0.5 x 0.3 m faces read as slabs at 10 m; the Bakery view is a wall of chunky bricks), stacks read as thin pillars, and the charred half-timber and plaster gables are not visible from any eye camera. Round-2 item for the shells (T13 or a shell kit round): smaller courses (target 0.25-0.35 m long stones in the texture/UV scale or in the block size), a darker soot band at the top, and turn the gable frames to face the lanes.
- Mill camera now shows the mill frame against the sky (+4), but the meadow under it is still the pale flat ground.
- Collision deviation (accepted): shell Footing and Chimney carry no MeshCollider; the boxes are the collision (walls to about 2.7 m, stack full height), shells are scaled 0.95 in plan so R2_01_North stays reachable. Interior rubble is walkable.

## Baseline verdict (R0, Ruined, Morning; strict /40)
Seen on sheet B: LowerLane 16, RingWest 15, RingSouth 14, Orchard 10, Mill 11, Meadow 8, Lake 13, JettyBack 12, RoadOut 17, RoadBack 15. From Run 6 (same views): Spawn 22, Postcard 25, West 22, Climb 20, BellYard 17, Balcony 22. Not seen: Bakery, InnFront, HillWest (HillWest is nearly black: p50 0.08; it looks straight at the morning-shaded crag from (-38, 23): retarget it to look east-south-east along the church wall, or move it to about (-34, 20)).
What the new cameras show:
1. **Ground outside the wall is the worst thing in the area.** Meadow, shore and road verges are a flat pale yellow (p50 .72-.80) with sparse, oversized grass clumps (single blades near a metre wide). It needs a darker, richer grass layer, dense short grass with tall clumps only as accents, and Z5 exposure down.
2. **The bank under the town wall is a pale tiled slope**: the terrain's steep rule paints the cobble layer on it (Meadow, JettyBack, Lake). It should be earth and grass with shrubs and rocks. This is also the "pale bank" Run 6 could not find.
3. **From the lake the town has no skyline** (JettyBack): wall and gate only. Tall shells on the southern outer lots fix this.
4. Ring 2's low ruin stubs read as dark stacked boxes (RingSouth, RingWest); lanes are clean cobbles with nothing on them.
5. Orchard camera stands in deep shade under two trees whose bark renders blue-black (p50 .16); the strip is bare.
6. Mill camera faces the mill's shadow wall from 12 m; the mill reads as a dark block. Move the camera back and south so the wheel and stream are in frame, and lift the shade fill.
7. Hedge leaves on the road are far too large (they read as cabbages): scale the hedge texture or mesh down by about 3.
8. High_Lake and High_East render with fog on (only names starting `Cap_HM_Overview` get fog off in `HollowmereCapture`): add `Cap_HM_High` to that rule.
9. RoadOut already has the shrine, fences and trees and is the best of the new views (17).

## Order (2026-11-16, full list restored)
1. **T03** scene diet (resumed).
2. **T05** shells into the town, plus moving the staged nature set into `Assets/` in the same run.
3. **T08** ground and shore, grown after the Abbey study (plan 1b, points 1, 2, 7): fine-blade grass carpet with per-clump colour for the whole outside, splat breakup, bank fix, reed beds, shore rocks, boats, meadow flowers (N06, N07), tree bases, the Mill and HillWest camera fixes, hedge scale, fog-off for the High cameras. Probably two runs (ground, then shore and mill).
4. **T06** streets (decals, wall-foot strips, rubble spill, clutter clusters, story props).
5. **T07** orchard and allotments.
6. **T09** bell yard, churchyard, crag variety (N01-N03).
7. **T10** road out, boundary, hill edge.
8. **T11** atmosphere: light zones, fills, shafts, fog planes, highlight shoulder, ambient life, mountain reshape.
9. **T12** LOD and cull pass, APV trial.
10. **T13** round 2 where a district is under 25; **T14** landing.

## What T05 needs to know (from T02's report)
- Staged pieces: `Ruin_<S|M|L>_<A|B|C>_<Mid|Tall>.fbx` (18), `Headstone_A/B/C`, `Signpost_A`, `Hatch_Oak`. Tall max heights S 6.7-7.7 m, M 7.8-8.4 m, L 8.9-9.3 m; Mid 4.8-6.6 m. Triangles 2.9k-5.7k. No LODs, no foliage child (the low kit has one).
- Contract kept from the low kit (`sc_ruin_kit.py`): root `Ruin_<C>_<V>...` with children `Footing`, `Chimney`, `FOOTPRINT`, `SOCKET_Doorstep`, `SOCKET_Smoke`; slots `FK_Sandstone`, `FK_ChimneyStone`, `FK_PlinthJoint`, `FK_Oak`, plus `FK_Plaster` (new on shells; `BeautyCornerArt.BuildFacadeMaterials` already maps it). UV0 box projection 2 m per tile; corner colour attribute `wear` (R char, G moss, B polish).
- Collision boxes are meshes named `FOOTPRINT_COL_<n>` (11-27 per shell). `PlaceRuin` hides renderers whose name starts with `FOOTPRINT`; the Unity task must turn these boxes into BoxColliders (and not render them).
- `HollowmereRuinDressing` leans the "Chimney" node another 2.5-4 degrees on top of the 1-3 baked in: cap the extra lean for shells (the frame members do not follow it).
- The low kit's stacks are 3.0 / 3.8 / 4.6 m; its walls 1.1-1.4 m.
- Height class per lot (the low class is today's kit):
  - Ring 1: F01 Low; F02 Mid; F03 Tall; F04 Mid; F05 Tall; F06 Tall; F07 Tall; F08 Mid; F09 Tall; F10 Mid.
  - Ring 2 inner: R2_01 Mid; 02 Tall; 03 Mid; 04 Tall; 05 Tall; 06 Mid; 07 Low; 08 Tall; 09 Mid.
  - Ring 2 outer: R2_10 Low; 11 Tall; 12 Mid; 13 Tall; 14 Mid; 15 Tall; 16 Low (Market Street ends in sky); 17 Tall; 18 Low; 19 Tall; 20 Mid; 21 Tall; 22 Low; 23 Mid.
  - Ring 3: R3_01 Mid; R3_02 Low; R3_03 Mid.
  - Rules behind it: never two Tall side by side on one lane side; F01 low so spawn sees the bakery lot; R2_15 and R2_17 tall for the skyline from the lake.
- Core's `RuinVariant` (A, B, C by table order) stays; the class only picks the `_Mid` / `_Tall` file. Layout tests pin lot data: do not change Core.
- Sheet after T05: cameras Spawn, West, Bakery, InnFront, Postcard, Balcony, MarketStreet, LowerLane, RingWest, RingSouth, JettyBack (one sheet, R0 as insets).

## T03 brief in short (if it must be restarted)
Move generated meshes out of `Hollowmere.unity` into assets under `Assets/_Project/Content/Generated/Hollowmere/` through one helper; the bulk is `RD_Cards_Ring` (8.9 MB, `HollowmereRoad.Cards` -> `EmitTiles`), `RD_HillCards_Ring` (5.5 MB, `HollowmereWild.cs` about :316), `LK_StoneTread_*` (4.9 MB, 220 meshes, `HollowmereLanes.BuildTreadMesh`: share by dimensions), the backdrop layers (about 4 MB). LFS rule for the generated `.asset` files. Build twice with no diff; three-camera image difference against `R0/`; `gate`; push. Target under 40 MB.

## Decisions (2026-11-16, current direction)
- The Ruined preset in Morning light is the judged state; Rebuilt gets one sanity sheet at the end.
- Ruin shells are whole replacement ruin meshes in three height classes per lot, stone below and charred half-timber with plaster above.
- Orchard trees are TR02/TR03 with a yellow-green leaf variant; no new apple tree model this run.
- Ground cover goes into terrain detail layers with district masks, not scene objects.
- APV is a late bake trial, now on the landing list.
- No Abbey frames can be fetched (the coordinator confirmed); the reference row is our concepts H1-H8.

## Facts found (2026-11-16)
- Scene 59.6 MB: meshes 24.7 MB (41%), MeshRenderers 10.6 MB, PrefabInstances 8.5 MB; lanes hold about 1,850 objects in each preset.
- `SC_CAPTURE_ONLY` is a substring match: `HM_Overview` also shoots OverviewW and OverviewE.
- Capture with a moved camera needs a rebuild; a transient IOException 1224 on scene save passed on retry.
- PIL is not importable with `python -I` on this PC; run the sheet tool without `-I`.
- Blender 5.2.2 is the installed version (the kit was written on 4.5.3; it runs).
- `git push -u origin claude/area1-world` was needed once; plain `git push` works now.

## Lessons so far
- Two or three agents at once filled the 5-hour window in 100 minutes and the limit cut two of them mid-run. One at a time, and STATUS before each launch.
- The Haiku scans cost 310k tokens for three reports: worth it once, but ask for less next time (the lot table and the builder list were the parts used).
- Looking at the Blender sheet before the Unity round caught the all-stone, thin-stack kit for the price of one image.
