State: IN PROGRESS
Session: Q14. Decorations batch 2: market and lane clutter
Progress: 15%
Sheet: none yet
Blocker: paused by coordinator for usage

Score: -
Tris: -
SC RESULT: -
Merge commit: -

Paused 2026-10-31 at 15%: both agents told to push their WIP and stop; resume picks up from their pushed commits.
Plan: CONTRACT.md (asset names, footprints, pivots, CL_* slots on shared B1 tileables, no bakes). Two agents running:
- art: Art/Blender/town/sc_clutter.py (handcart, crate/sack stacks, rain barrel, flower box, well, 6 trade signs, washing-line parts; each with a ruined look), export to Content/Art/Town/Clutter, contact sheet with references.
- Unity: Core HollowmereClutter (ring 1/2 rows, Validate incl. path graph), EditMode tests, Editor HollowmereClutter placement + Cap_HM_Clutter.
Then: merge origin/main, build scene, capture Cap_HM_Clutter, unity.sh check, merge.

## Resume notes
- Unity agent (WIP 658bfcf): Core HollowmereClutter.cs pushed (ClutterPlan + Validate, path graph rule in). Rebuilt 50 ground rows + 9 signs + 2 washing spans, Ruined 11.
  Open: CrateStack_Broken/SackStack_Broken missing in Ruined; washing spans 2 (want 3-5); signs may double the Bakery/Inn Part_Sign; Inn footprint assumed 9.0 x 6.5.
  Next: tests (HollowmereClutterTests), Editor HollowmereClutter + one-line BuildScene call + Cap_HM_Clutter, cloud compile + core tests.
- Art agent: waiting for its hand-back.

## Usage waste
- (filled at the end)
