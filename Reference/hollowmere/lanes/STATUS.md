# Q11 lane kit: status

State: DONE
Archive: ready
Session: Q11. Lane kit: lane walls, steps and lane dressing
Progress: 100%
Sheet: Reference/hollowmere/lanes/SHEET_lanes_unity.jpg (kit sheet: Reference/hollowmere/lanes/SHEET_lane_kit.jpg)
Blocker: none

Score: Unity captures about 28/40 (Market Street 29, West Lane mouth 28, Ring Lane SW steps 26; first 26, target 31 not reached, stopped at the
  round limit). Kit sheet: self 29.5/40 after the coordinator's fix round (coordinator read ~26 before it).
Tris: kit 19,066 (17 pieces, all within budget); 1326 placement rows (Ruined 1059, Rebuilt 1061 pieces).
SC RESULT: SC RESULT check PASS on f578e61 (build PASS, test-edit 711/711, test-play 32 passed / 8 skipped / 0 failed); the merge's tree equals the checked tip
Merge commit: 6ce70f2

Result:
- Art: Art/Blender/town/sc_lane_kit.py -> Content/Art/Town/Lanes/LaneKit/LaneKit.fbx + SOURCES.md (LK_* slots on the shared B1 tileables): garden
  wall/broken/end, retaining wall/low/breach/buttress, lane step, kerb, gutter/choked/grate, wall lantern/broken, gate pier, gate leaf/broken.
- Placement: Core LaneKitPlan (pure C#, golden-tested): stepped flights on every lane segment >= 0.18 grade (all within the 20 deg rule, risers
  <= 0.2 m and under the player step offset), kerbed ramps 0.05-0.18, gutters with grates, garden walls with yard gates on free lane edges, kit
  retaining walls replacing the greybox terrace lines and the lot cut/fill boxes (stacked to full height), lanterns ~15 m (lit in Rebuilt, broken
  in Ruined). Ring 2 lanes only; clear of the Q8 wall, the keep, lots, landmarks and path edges. Editor placer HollowmereLanes (art or greybox).
- Cameras: Cap_HM_MarketStreet (low on the street looking up the flight), Cap_HM_LaneMouthWest, Cap_HM_TerraceSteps.
- Checked first (user rule): the hill-town stepped street for >13 deg lanes; Q8's TownWallPlan/HollowmereTownWall and Q9's keep terrace kit as
  templates; no external tool fits the layout-row builder.
- Weak / next visible gains: the gutter still reads dark and thin at mid distance (Ring Lane SW); gate paint pale grey-green; retaining wall flat
  tan at range; moss cushions on wall tops; warmer ochre stones in the rubble.

History:
- 2026-10-31 started on branch claude/zen-feynman-991yav (main merged in). Read the spec (2, 5, 7), the brief, HollowmereLayout lanes, the builder's
  greybox terrace walls (BuildWalls TerraceWall/LowerTerraceWall) and lot CutAndFill boxes, and Q9's sc_keep_terrace.py (template + contract style).
- Measured the lanes (dotnet probe on Core): every stub has 6-8 m segments at grade 0.24-0.30 (13-17 deg, under the 20 deg rule); the Ring Lane is flat
  except two 0.24 drops in the south-west and south-east; greybox terrace walls total about 77 m (shelf edge) + 23 m (lower terrace).
- Plan (paused before dispatch): CONTRACT.md for `Art/Blender/town/sc_lane_kit.py` (root LaneKit: GardenWall/End/Broken, RetainWall/Low/Breach,
  RetainButtress, LaneStep, LaneKerb, Gutter/Choked/Grate, WallLantern/Broken, GatePier, GateLeaf/Broken; Q9 axis and material conventions) on a Sonnet
  Blender agent; in parallel a Sonnet implementer for Core `HollowmereLanes` rows (steps on segments >= 0.18 grade, kerbed ramps 0.05-0.18, gutters,
  garden walls with gates on free lane edges, kit retaining walls replacing the greybox terrace lines and lot cut/fill boxes, lanterns every ~15 m;
  Ruined = broken/choked/unlit variants) + tests + an Editor placer in its own file (minimal HollowmereBuilder touch), ring 2 lanes only (not Mill Lane,
  Saltwake road or Q8's town wall). Captures: Market Street, West Lane mouth, Ring Lane south-west terrace steps.
- Checked for existing practice: the stepped street / cordonata pattern of hill towns for >13 deg grades; Q9's terrace kit as the code template;
  no off-the-shelf lane-dressing tool fits the layout-row builder pattern. Unity editor was still downloading at pause time.
- Resumed after the usage pause; merged main (Q8 town wall ca383a8). Contract written: Reference/hollowmere/lanes/CONTRACT.md. Dispatching the Blender
  kit agent and the placement implementer (both Sonnet) in parallel. Q8's TownWallPlan + HollowmereTownWall are the placement template.
- Both agents hit the 5-hour limit, were resumed after the reset, then paused again by the coordinator (usage). Each commits its WIP (art on its worktree
  branch, placement on this branch) before stopping; on RESUME they continue from their transcripts.
- Resumed (coordinator). Placement WIP a9fce01 (LaneKitPlan.cs, uncompiled); art not started. Both agents continue. Contract note: retaining walls and
  lanterns are tested as "Ground lies between base and coping" (they are sunk or wall-mounted by design), not "Y within 0.25 m".
- Art merged (3c40274 via fa3e10b): sc_lane_kit.py, LaneKit.fbx, SOURCES.md (LK_* slots), sheet. Placement pushed (dfe90ed): Core LaneKitPlan
  (1326 rows, golden), 7 LaneKitPlanTests, Core suite 310/310; HollowmereLanes placer + builder wiring (terrace Wall lines and CutAndFill replaced),
  cameras Cap_HM_LaneMouthWest, Cap_HM_TerraceSteps (+ existing Cap_HM_MarketStreet). Editor code not yet Unity-compiled: the Unity download had died
  at a container restart; restarted, waiting for it.
- Kit fix round done (coordinator's two fixes + moss; self 29.5/40; the flat slab was the review backdrop, replaced by real RetainWall modules; gate leaf
  planks + straps), on worktree branch commit 362d0a3, merged after the Unity integration agent (compile fixes, build, SC_CAPTURE_ONLY captures) reports.
- Unity integration: compile fixed, LK_* slots matched to SOURCES.md, nographics build with scene committed (8c17ed2), first capture set of the 3 cameras x 2 presets taken, a camera fix round in progress (0cc7bb2).
