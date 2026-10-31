State: IN PROGRESS
Session: Q14. Decorations batch 2: market and lane clutter
Progress: 80%
Sheet: Reference/hollowmere/clutter/clutter_engine_sheet.jpg
Blocker: none

Score: art rounds (Blender sheet, visible faces): r1 27 -> r2 28.5 -> r3 29 (agent); controller read about 28/40 (flower clumps read as pastel blobs, cloth pale). Stopped at round 3 (gain < 1.5); in-engine check next.
Tris: props 994-2712 (Well 2634), signs 580-1340, washing-line parts 36-384; all within CONTRACT budgets.
SC RESULT: -
Merge commit: -

Resumed 2026-10-31 after the usage pause; origin/main merged (45da48c: garden, keep, town look).
Plan: CONTRACT.md (asset names, footprints, pivots, CL_* slots on shared B1 tileables, no bakes). Two agents running:
- art: Art/Blender/town/sc_clutter.py (handcart, crate/sack stacks, rain barrel, flower box, well, 6 trade signs, washing-line parts; each with a ruined look), export to Content/Art/Town/Clutter, contact sheet with references.
- Unity: Core HollowmereClutter (ring 1/2 rows, Validate incl. path graph), EditMode tests, Editor HollowmereClutter placement + Cap_HM_Clutter.
Then: merge origin/main, build scene, capture Cap_HM_Clutter, unity.sh check, merge.

## Progress
- Art (7231bf9): Art/Blender/town/sc_clutter.py, 21 FBX in Content/Art/Town/Clutter (Gen, slot materials on shared tileables, no bakes), SOURCES.md.
  Deviations: a few meshes overhang their FOOTPRINT by up to 0.14 m (RainBarrel, FlowerBox, SackStack_Broken, Well_Ruined); signs reach 0.95 m; painted_wood is blue-grey, so CL_Paint/PaintRed may read duller in Unity.
- Unity (740e0a5): Core HollowmereClutter (rows outside lots: clear of garden/keep), Rebuilt 53 ground rows + 6 signs (4 workshops, tailor F06, grocer F07) + 3 washing spans, Ruined 15; HollowmereClutterTests 13 green on the Core runner (path node / edge negative tests). Editor placement written, not yet compiled (Unity editor still downloading in this container).
- Coordinator: sheet r3 ~27/40 APPROVED, no more art rounds. Later polish: sacks are smooth pillows (no burlap weave/stitching), flower clumps pastel blobs, washing-line cloth pale.
- 2026-10-31: Unity editor finally extracted in this container (the download stalled ~45 min); origin/main merged (lake, stair). In-engine step starting.
- In-engine (9a7f545): build PASS, log `SC HOLLOWMERE clutter: rebuilt rows=53 ruined rows=15 signs=6 washing lines=3 | art rows=68 greybox rows=0 ... tris=158092`; footprints raised to the real art extents; Validate empty, Core tests 317/317. CL_Paint/PaintRed moved to plaster (painted_wood read teal). Capture: pieces sit on the ground, workshop sign on its wall; washing line not in frame yet.
- Now: Cap_HM_Clutter re-aimed at the R2_19-R2_20 washing line, one capture, then merge origin/main and one check.
- Old next: compile, footprints to real extents, scene build, capture Cap_HM_Clutter only, in-engine sheet, then check + merge.

## Usage waste
- (filled at the end)
