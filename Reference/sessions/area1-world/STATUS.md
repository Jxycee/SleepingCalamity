# Area 1 world (Fable Run 7) STATUS

State: **building; handoff written at 252k context (2026-11-16 21:05 Central)**. A restarted session needs only this file and the plan `docs/design/area1-world-build.md`. Brief: `side-session-notes/local_88bfb1c1-.../run7-fable-whole-area1-brief.md`.
Branch `claude/area1-world` (from `origin/claude/area1-rework` 44a12aa2), worktree `.claude/worktrees/elated-dubinsky-d77725`, upstream set (`git push` works).
Scratch (a reboot clears it): `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\0975e27d-5568-4784-b3db-e3319868a89d\scratchpad\run7`.
Code Mode: not offered in a Desktop session.

## Usage
- Start (17:30): 5-hour 45%, weekly 64%, Fable 9%.
- The 5-hour limit hit at about 19:10 and cut T03 and T04 mid-run. After the reset (21:02): 5-hour 14%, weekly 74%, Fable 20%.
- So the first window cost 10 weekly points for: three Haiku scans (310k tokens), the plan, T01 (156k), T02 in two rounds (280k), T04 (finished and committed before the cut), a few minutes of T03.
- Budget left to the 92% stop: about 18 points, roughly seven implementer runs with their judging. **Run one implementer at a time** (parallel runs filled the window in 100 minutes) and cut the list as below.
- Images used by the controller: 3 of 16 (Run 6 R4 sheet, shell kit sheet, `R0_sheet_B.jpg`). `R0_sheet_A.jpg` (cameras 1-10) has not been looked at.

## Task progress
| Task | State | Commit | Notes |
|---|---|---|---|
| Plan (Phase 1) | done | 74fa409f | 11 districts, 20 eye cameras, rules, tasks |
| T01 cameras, sheet tool, baseline, scene composition | done, gate PASS 1859/1859, pushed | 4dbeb2f3 | `R0/` JPGs and sheets A/B; `tools/sheet_area.py`, `tools/scene_composition.py`; `scene-composition.md` |
| T02 ruin shell kit | done (2 rounds), not yet in Unity | 34568e7b, 2ce1fa03 | `Art/Staging/area1-world/shells/` (23 FBX), generator `Art/Blender/town/sc_ruin_shells.py` |
| T04 nature set | done by its commit; its final report was cut by the limit, so unreviewed | 75329ede | `Art/Staging/area1-world/nature/N01..N07` and `SOURCES.md`; contact sheet in scratch `nature\contact_sheet.png` (not looked at) |
| T03 scene diet | cut by the limit after a few minutes, nothing changed on disk; being resumed | | brief below |
| T05 onward | not started | | revised order below |

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

## Revised order (2026-11-16, for about 18 weekly points)
1. **T03** scene diet (resumed).
2. **T05** shells into the town, plus moving the staged nature set into `Assets/` in the same run.
3. **T08+** ground and shore: grass layer colour and density for the whole outside, bank splat fix, reed beds, shore rocks, boats, meadow flowers (N06, N07), the Mill and HillWest camera fixes, hedge scale, fog-off for the High cameras.
4. **T06** streets (decals, wall-foot strips, clutter clusters, story props), slim.
5. **T07** orchard and allotments.
6. **T09** bell yard, churchyard, crag variety (N01-N03).
7. **T11** atmosphere, slim: light zones Z2, Z4, Z5, Z7, shade fill, highlight shoulder.
8. **T14** landing.
Dropped to the landing session's list unless usage allows: T10 (road boundary and hill edge floor), T12 (LOD and cull pass, APV trial), the mountain reshape, a second round for every district. One judged sheet per task; a second round only where a district is under 20.

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
